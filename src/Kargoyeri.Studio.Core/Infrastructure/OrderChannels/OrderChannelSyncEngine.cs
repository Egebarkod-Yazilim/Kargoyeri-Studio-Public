using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;
using Kargoyeri.Studio.Core.Models;
using Kargoyeri.Studio.Core.Infrastructure;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels;

/// <summary>
/// Multi-channel sipariş ingestion orkestratoru — CANLI.
/// CustomerProfile.Metadata'dan aktif kanallari okur, her birinin adapter'ini cagirir,
/// donen RawIncomingOrder'lari shipment-creation pipeline'a aktarir.
///
/// MarketplaceOrderSyncEngine ile paralel calisir: bu engine multi-channel (yeni)
/// metadata namespace'inden (channel.{code}.{field}) cekilenleri yonetir.
/// Idempotency, adres dogrulama, anomali tespiti dahil tum production akisi var.
/// </summary>
public sealed class OrderChannelSyncEngine
{
    private readonly CustomerService _customerService;
    private readonly OrderChannelRegistry _registry;
    private readonly OrderChannelSyncMonitor _monitor;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IOperationLogRepository _operationLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ShipmentAddressValidationService _addressValidationService;
    private readonly ShipmentAnomalyDetectionService _anomalyDetectionService;
    private readonly ILogger<OrderChannelSyncEngine> _logger;

    public OrderChannelSyncEngine(
        CustomerService customerService,
        OrderChannelRegistry registry,
        OrderChannelSyncMonitor monitor,
        IShipmentRepository shipmentRepository,
        IOperationLogRepository operationLogRepository,
        IUnitOfWork unitOfWork,
        ShipmentAddressValidationService addressValidationService,
        ShipmentAnomalyDetectionService anomalyDetectionService,
        ILogger<OrderChannelSyncEngine> logger)
    {
        _customerService = customerService;
        _registry = registry;
        _monitor = monitor;
        _shipmentRepository = shipmentRepository;
        _operationLogRepository = operationLogRepository;
        _unitOfWork = unitOfWork;
        _addressValidationService = addressValidationService;
        _anomalyDetectionService = anomalyDetectionService;
        _logger = logger;
    }

    public async Task<OrderChannelSyncRun> SyncChannelAsync(
        string tenantKey,
        OrderChannelType channel,
        bool includeSinceLast,
        CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var run = new OrderChannelSyncRun
        {
            TenantKey = tenantKey,
            Channel = channel,
            StartedAtUtc = startedAt
        };

        try
        {
            var profile = await _customerService.GetProfileAsync(tenantKey, ct);
            if (profile is null)
            {
                run.Success = false;
                run.Message = $"Tenant bulunamadi: {tenantKey}";
                return Finalize(run);
            }

            var state = OrderChannelMetadata.Read(profile, channel);
            var descriptor = OrderChannelCatalog.Find(channel);

            if (descriptor is null)
            {
                run.Success = false;
                run.Message = $"Kanal tanimsiz: {channel}";
                return Finalize(run);
            }

            if (descriptor.Stage == OrderChannelStage.ComingSoon)
            {
                run.Success = false;
                run.Message = $"{descriptor.DisplayName} bu surumde aktif degil.";
                return Finalize(run);
            }

            if (!state.Enabled)
            {
                run.Success = false;
                run.Message = $"{descriptor.DisplayName} kanali kapali.";
                return Finalize(run);
            }

            if (!state.HasRequiredFields(descriptor))
            {
                run.Success = false;
                run.Message = $"{descriptor.DisplayName} icin zorunlu API bilgileri eksik.";
                return Finalize(run);
            }

            if (!_registry.TryGet(channel, out var adapter))
            {
                run.Success = false;
                run.Message = $"{descriptor.DisplayName} icin adapter kayitli degil.";
                return Finalize(run);
            }

            var since = includeSinceLast && state.LastSyncUtc.HasValue
                ? state.LastSyncUtc.Value
                : DateTimeOffset.UtcNow.AddHours(-24);

            var creds = new OrderChannelCredentials(tenantKey, channel, state.Fields);
            var fetchResult = await adapter.FetchOrdersAsync(creds, since, ct);

            run.Success = fetchResult.Success;
            run.Message = fetchResult.Message;
            run.FetchedCount = fetchResult.Orders.Count;
            run.IsSimulation = fetchResult.IsSimulation;

            // ── Shipment-creation pipeline (CANLI) ───────────────────────────────
            // RawIncomingOrder'lari CargoShipment'a cevirip idempotent kaydet.
            if (fetchResult.Success && fetchResult.Orders.Count > 0)
            {
                // Provider cozumu: state.DefaultProvider varsa onu kullan; yoksa Sandbox.
                CargoProviderTypeDto resolvedProviderDto = CargoProviderTypeDto.Sandbox;
                if (!string.IsNullOrWhiteSpace(state.DefaultProvider)
                    && Enum.TryParse<CargoProviderTypeDto>(state.DefaultProvider, true, out var dp))
                {
                    resolvedProviderDto = dp;
                }
                var resolvedProvider = (CargoProviderType)resolvedProviderDto;

                // Legacy sender bilgileri (varsa) — WorkspaceFeatureMetadata yolu ile
                var legacy = WorkspaceFeatureMetadata.ReadMarketplace(profile);
                var sender = BuildSender(profile, legacy);

                int imported = 0, skipped = 0;
                foreach (var raw in fetchResult.Orders)
                {
                    if (ct.IsCancellationRequested) break;

                    var sourceChannel = (OrderSourceChannel)(int)raw.SourceChannel;
                    var idempotencyKey = $"{tenantKey}|{OrderSourceChannelCodes.GetPrefix(sourceChannel)}|{raw.ChannelExternalOrderId}";

                    var existing = await _shipmentRepository.GetByIdempotencyKeyAsync(tenantKey, idempotencyKey, ct);
                    if (existing is not null)
                    {
                        skipped++;
                        continue;
                    }

                    // ValidateAsync DTO uzerinde calisir; normalize edip sonra AddressInfo'ya cevirelim.
                    var recipientDto = new AddressDto
                    {
                        Name = string.IsNullOrWhiteSpace(raw.Recipient.FullName) ? $"{descriptor.DisplayName} musterisi" : raw.Recipient.FullName,
                        Phone = raw.Recipient.Phone,
                        Email = raw.Recipient.Email,
                        City = raw.Recipient.City ?? string.Empty,
                        District = raw.Recipient.District,
                        AddressLine1 = raw.Recipient.AddressLine ?? string.Empty,
                        PostalCode = raw.Recipient.PostalCode,
                        CountryCode = "TR"
                    };

                    var validation = await _addressValidationService.ValidateAsync(tenantKey, recipientDto, ct);
                    ShipmentAddressValidationService.ApplyNormalization(recipientDto, validation);

                    var recipientInfo = new AddressInfo
                    {
                        Name = recipientDto.Name,
                        Phone = recipientDto.Phone,
                        Email = recipientDto.Email,
                        City = recipientDto.City,
                        District = recipientDto.District,
                        AddressLine1 = recipientDto.AddressLine1,
                        PostalCode = recipientDto.PostalCode,
                        CountryCode = recipientDto.CountryCode ?? "TR"
                    };

                    var weight = raw.Items.Sum(i => i.WeightKg ?? 0);
                    if (weight <= 0) weight = 1;

                    var anomaly = await _anomalyDetectionService.AnalyzeAsync(tenantKey, new ShipmentAnomalyInput
                    {
                        Provider = resolvedProviderDto.ToString(),
                        RecipientCity = recipientInfo.City,
                        CollectionAmount = raw.CollectionAmount,
                        Weight = weight,
                        Desi = 1
                    }, ct);

                    var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["studio.origin"] = "order-channel-sync",
                        ["channel.code"] = descriptor.Code,
                        ["channel.externalOrderId"] = raw.ChannelExternalOrderId,
                        ["channel.orderNumber"] = raw.OrderNumber,
                        ["address.validationStatus"] = validation.StatusText
                    };
                    ShipmentAnomalyDetectionService.ApplyMetadata(metadata, anomaly);
                    if (!string.IsNullOrWhiteSpace(raw.Notes))
                        metadata["channel.notes"] = raw.Notes!;

                    var shipment = CargoShipment.Create(
                        shipmentReference: GenerateShipmentReference(tenantKey, sourceChannel),
                        tenantKey: tenantKey,
                        orderReference: raw.OrderNumber,
                        clientShipmentReference: raw.ChannelExternalOrderId,
                        idempotencyKey: idempotencyKey,
                        provider: resolvedProvider,
                        source: IntegrationSourceType.Marketplace,
                        collectionAmount: raw.CollectionAmount,
                        currencyCode: string.IsNullOrWhiteSpace(raw.CurrencyCode) ? "TRY" : raw.CurrencyCode,
                        sender: sender,
                        recipient: recipientInfo,
                        packages:
                        [
                            new PackageInfo
                            {
                                PackageSequence = 1,
                                Weight = weight,
                                Desi = 1,
                                Description = $"{descriptor.DisplayName} siparisi",
                                CashOnDeliveryAmount = raw.CollectionAmount
                            }
                        ],
                        metadata: metadata,
                        sourceChannel: sourceChannel,
                        sourceChannelCode: OrderSourceChannelCodes.GetCode(sourceChannel));

                    await _shipmentRepository.UpsertAsync(shipment, ct);
                    await _operationLogRepository.AppendAsync(new ShipmentOperationLog
                    {
                        TenantKey = tenantKey,
                        ShipmentReference = shipment.ShipmentReference,
                        Operation = CargoOperationType.CreateShipment,
                        Severity = LogSeverity.Information,
                        Message = $"{descriptor.DisplayName} siparisi otomatik cekildi (channel-sync)."
                    }, ct);
                    await _unitOfWork.CommitAsync(ct);
                    imported++;
                }

                run.ImportedCount = imported;
                run.SkippedCount = skipped;
                run.Message = $"{descriptor.DisplayName}: {fetchResult.Orders.Count} cekildi, {imported} kaydedildi, {skipped} idempotent atlandi.";
            }

            _logger.LogInformation(
                "OrderChannel sync: tenant={Tenant} channel={Channel} fetched={Count} imported={Imp} skipped={Skp} sim={Sim}",
                tenantKey, channel, run.FetchedCount, run.ImportedCount, run.SkippedCount, run.IsSimulation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OrderChannel sync hatasi: tenant={Tenant} channel={Channel}", tenantKey, channel);
            run.Success = false;
            run.Message = "Sync sirasinda beklenmedik bir hata olustu: " + ex.Message;
        }

        return Finalize(run);
    }

    public async Task<IReadOnlyList<OrderChannelSyncRun>> SyncAllEnabledAsync(string tenantKey, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null) return Array.Empty<OrderChannelSyncRun>();

        var states = OrderChannelMetadata.ReadAll(profile);
        var runs = new List<OrderChannelSyncRun>();
        foreach (var state in states.Where(s => s.Enabled))
        {
            var run = await SyncChannelAsync(tenantKey, state.Channel, includeSinceLast: true, ct);
            runs.Add(run);
        }
        return runs;
    }

    private OrderChannelSyncRun Finalize(OrderChannelSyncRun run)
    {
        run.CompletedAtUtc = DateTimeOffset.UtcNow;
        _monitor.Record(run);
        return run;
    }

    private static AddressInfo BuildSender(CustomerProfileDto profile, MarketplaceWorkspaceSettings legacy)
    {
        // Legacy workspace marketplace sender bilgileri tum kanallar icin ortak sender olarak kullanilir.
        return new AddressInfo
        {
            Name = !string.IsNullOrWhiteSpace(legacy.SenderName) ? legacy.SenderName! : profile.Name,
            Phone = legacy.SenderPhone ?? "",
            City = legacy.SenderCity ?? "Istanbul",
            District = legacy.SenderDistrict,
            AddressLine1 = legacy.SenderAddress ?? "Merkez depo",
            CountryCode = "TR"
        };
    }

    /// <summary>
    /// Sistem-ici shipment referansi: kanal prefix'i ile disambigue (ornek: KY-ACME-ET-SHP-...).
    /// </summary>
    private static string GenerateShipmentReference(string tenantKey, OrderSourceChannel channel)
    {
        var compactTenant = new string(tenantKey.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(compactTenant)) compactTenant = "tenant";
        var prefix = OrderSourceChannelCodes.GetPrefix(channel);
        var candidate = $"KY-{compactTenant.ToUpperInvariant()}-{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return candidate.Length > 48 ? candidate[..48] : candidate;
    }
}

public sealed class OrderChannelSyncRun
{
    public string TenantKey { get; set; } = string.Empty;
    public OrderChannelType Channel { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int FetchedCount { get; set; }

    /// <summary>Bu sync run sirasinda gercekten DB'ye kaydedilen yeni shipment sayisi.</summary>
    public int ImportedCount { get; set; }

    /// <summary>Idempotency dolayisiyla atlanan (zaten kayitli) shipment sayisi.</summary>
    public int SkippedCount { get; set; }

    public bool IsSimulation { get; set; }
}

/// <summary>
/// Son N sync run'i in-memory tutar (UI'da gostermek icin).
/// </summary>
public sealed class OrderChannelSyncMonitor
{
    private readonly object _lock = new();
    private readonly LinkedList<OrderChannelSyncRun> _runs = new();
    private const int MaxRuns = 200;

    public void Record(OrderChannelSyncRun run)
    {
        lock (_lock)
        {
            _runs.AddFirst(run);
            while (_runs.Count > MaxRuns) _runs.RemoveLast();
        }
    }

    public IReadOnlyList<OrderChannelSyncRun> Recent(string? tenantKey = null, int take = 20)
    {
        lock (_lock)
        {
            return _runs
                .Where(r => tenantKey is null || r.TenantKey == tenantKey)
                .Take(take)
                .ToList();
        }
    }
}
