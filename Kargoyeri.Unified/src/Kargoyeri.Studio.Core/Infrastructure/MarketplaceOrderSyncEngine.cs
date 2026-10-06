using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;
using Kargoyeri.Studio.Core.Infrastructure.OrderChannels;
using Kargoyeri.Studio.Core.Models;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class MarketplaceOrderSyncEngine
{
    private readonly CustomerService _customerService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IOperationLogRepository _operationLogRepository;
    private readonly Kargoyeri.Application.Abstractions.Persistence.IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ShipmentAddressValidationService _addressValidationService;
    private readonly ShipmentAnomalyDetectionService _anomalyDetectionService;
    private readonly MarketplaceSyncMonitor _monitor;
    private readonly OrderChannelRegistry _channelRegistry;
    private readonly ILogger<MarketplaceOrderSyncEngine> _logger;

    public MarketplaceOrderSyncEngine(
        CustomerService customerService,
        IShipmentRepository shipmentRepository,
        IOperationLogRepository operationLogRepository,
        Kargoyeri.Application.Abstractions.Persistence.IUnitOfWork unitOfWork,
        IHttpClientFactory httpClientFactory,
        ShipmentAddressValidationService addressValidationService,
        ShipmentAnomalyDetectionService anomalyDetectionService,
        MarketplaceSyncMonitor monitor,
        OrderChannelRegistry channelRegistry,
        ILogger<MarketplaceOrderSyncEngine> logger)
    {
        _customerService = customerService;
        _shipmentRepository = shipmentRepository;
        _operationLogRepository = operationLogRepository;
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _addressValidationService = addressValidationService;
        _anomalyDetectionService = anomalyDetectionService;
        _monitor = monitor;
        _channelRegistry = channelRegistry;
        _logger = logger;
    }

    public async Task<MarketplaceSyncRun> SyncTenantAsync(string tenantKey, bool force, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var settings = WorkspaceFeatureMetadata.ReadMarketplace(profile);
        var run = new MarketplaceSyncRun
        {
            TenantKey = tenantKey,
            Platform = settings.Platform,
            Success = true
        };

        if (!settings.HasConfiguration)
        {
            run.Success = false;
            run.Message = "Marketplace entegrasyon ayarlari eksik.";
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            _monitor.Record(run);
            return run;
        }

        if (!force && !settings.AutoSyncEnabled)
        {
            run.Message = "Auto sync kapali oldugu icin atlandi.";
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            _monitor.Record(run);
            return run;
        }

        try
        {
            var orders = await FetchOrdersAsync(settings, cancellationToken);
            run.PulledCount = orders.Count;

            foreach (var order in orders)
            {
                var idempotencyKey = $"marketplace:{settings.Platform}:{order.OrderReference}";
                var existing = await _shipmentRepository.GetByIdempotencyKeyAsync(tenantKey, idempotencyKey, cancellationToken);
                if (existing is not null)
                {
                    run.SkippedCount++;
                    continue;
                }

                var provider = ResolveProvider(profile, settings);
                if (provider is null)
                {
                    run.FailedCount++;
                    run.Success = false;
                    run.Message = "Marketplace siparisleri icin secilebilir provider bulunamadi.";
                    continue;
                }

                var recipient = new AddressDto
                {
                    Name = order.RecipientName,
                    Phone = order.RecipientPhone,
                    City = order.RecipientCity,
                    District = order.RecipientDistrict,
                    AddressLine1 = order.RecipientAddress
                };

                var validation = await _addressValidationService.ValidateAsync(tenantKey, recipient, cancellationToken);
                if (validation.HasBlockingIssues)
                {
                    run.FailedCount++;
                    run.Success = false;
                    continue;
                }

                ShipmentAddressValidationService.ApplyNormalization(recipient, validation);

                var anomaly = await _anomalyDetectionService.AnalyzeAsync(tenantKey, new ShipmentAnomalyInput
                {
                    Provider = provider.Value.ToString(),
                    RecipientCity = recipient.City,
                    CollectionAmount = order.CollectionAmount,
                    Weight = order.Weight,
                    Desi = order.Desi
                }, cancellationToken);

                var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["studio.origin"] = "marketplace-sync",
                    ["marketplace.platform"] = settings.Platform ?? string.Empty,
                    ["marketplace.orderReference"] = order.OrderReference,
                    ["marketplace.packageReference"] = order.PackageReference ?? string.Empty,
                    ["address.validationStatus"] = validation.StatusText
                };
                ShipmentAnomalyDetectionService.ApplyMetadata(metadata, anomaly);

                var resolvedChannel = ResolveSourceChannel(settings.Platform, order.SourceChannel);
                // Sistem-ici kanonik kod (mn / ty / hb / et-shp ...) — UI rozet & filtre bunu kullanir.
                var resolvedCode = OrderSourceChannelCodes.GetCode(resolvedChannel)
                                   ?? settings.Platform?.ToLowerInvariant();

                var shipment = CargoShipment.Create(
                    shipmentReference: GenerateShipmentReference(tenantKey, resolvedChannel),
                    tenantKey: tenantKey,
                    orderReference: order.OrderReference,
                    clientShipmentReference: order.PackageReference,
                    idempotencyKey: idempotencyKey,
                    provider: (CargoProviderType)provider.Value,
                    source: IntegrationSourceType.Marketplace,
                    collectionAmount: order.CollectionAmount,
                    currencyCode: string.IsNullOrWhiteSpace(order.CurrencyCode) ? "TRY" : order.CurrencyCode,
                    sender: BuildSender(profile, settings),
                    recipient: new AddressInfo
                    {
                        Name = recipient.Name,
                        Phone = recipient.Phone,
                        City = recipient.City,
                        District = recipient.District,
                        AddressLine1 = recipient.AddressLine1,
                        CountryCode = recipient.CountryCode
                    },
                    packages:
                    [
                        new PackageInfo
                        {
                            PackageSequence = 1,
                            Weight = order.Weight <= 0 ? 1 : order.Weight,
                            Desi = order.Desi <= 0 ? 1 : order.Desi,
                            Description = "Marketplace siparisi",
                            CashOnDeliveryAmount = order.CollectionAmount
                        }
                    ],
                    metadata: metadata,
                    sourceChannel: resolvedChannel,
                    sourceChannelCode: resolvedCode);

                await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
                await _operationLogRepository.AppendAsync(new ShipmentOperationLog
                {
                    TenantKey = tenantKey,
                    ShipmentReference = shipment.ShipmentReference,
                    Operation = CargoOperationType.CreateShipment,
                    Severity = LogSeverity.Information,
                    Message = $"{settings.Platform} siparisi otomatik cekildi ve Pending olarak iceri alindi."
                }, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                run.ImportedCount++;
            }

            run.Message ??= run.ImportedCount > 0
                ? $"{run.ImportedCount} siparis iceri alindi."
                : "Yeni siparis bulunamadi.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Marketplace sync hatasi. Tenant={TenantKey}", tenantKey);
            run.Success = false;
            run.Message = ex.Message;
        }
        finally
        {
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            _monitor.Record(run);
        }

        return run;
    }

    private async Task<IReadOnlyCollection<MarketplaceOrderCandidate>> FetchOrdersAsync(
        MarketplaceWorkspaceSettings settings,
        CancellationToken cancellationToken)
    {
        // P5-#11 — Yeni Live adapter'lar (Pazarama, ÇiçekSepeti, Amazon, PttAvm, Modanisa, ...)
        // OrderChannelRegistry üzerinden gelir. settings.Platform değeri bir OrderChannelType'a
        // map edilebiliyorsa adapter'ı çalıştır; aksi halde eski generic HTTP yoluna düş.
        var bridged = TryMapToChannel(settings);
        if (bridged is { } bridge && _channelRegistry.TryGet(bridge.Channel, out var adapter))
        {
            var creds = new OrderChannelCredentials(
                TenantKey: "marketplace-sync",
                Channel: bridge.Channel,
                Fields: bridge.Fields);

            // Son 24 saat — MarketplaceSyncRun "since" alanı yok, generic engine de aynı pencereyi kullanıyor.
            var since = DateTimeOffset.UtcNow.AddHours(-24);
            var fetchResult = await adapter.FetchOrdersAsync(creds, since, cancellationToken);

            if (!fetchResult.Success)
            {
                _logger.LogWarning(
                    "Marketplace sync — adapter ({Channel}) basarisiz: {Message}",
                    bridge.Channel, fetchResult.Message);
                return Array.Empty<MarketplaceOrderCandidate>();
            }

            return fetchResult.Orders.Select(ConvertRawOrder).ToArray();
        }

        using var client = _httpClientFactory.CreateClient("studio-marketplace");
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.ApiBaseUrl);

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", settings.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(settings.ApiSecret))
        {
            request.Headers.TryAddWithoutValidation("X-Api-Secret", settings.ApiSecret);
        }

        if (!string.IsNullOrWhiteSpace(settings.StoreId))
        {
            request.Headers.TryAddWithoutValidation("X-Store-Id", settings.StoreId);
        }

        if (!string.IsNullOrWhiteSpace(settings.ChannelCode))
        {
            request.Headers.TryAddWithoutValidation("X-Channel-Code", settings.ChannelCode);
        }

        if (!string.IsNullOrWhiteSpace(settings.ApiKey) && !string.IsNullOrWhiteSpace(settings.ApiSecret))
        {
            var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.ApiKey}:{settings.ApiSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var nodes = ExtractOrderNodes(document.RootElement, settings.Platform);
        return nodes
            .Select(ParseOrder)
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(x => !string.IsNullOrWhiteSpace(x.OrderReference))
            .ToArray();
    }

    private static IReadOnlyCollection<JsonElement> ExtractOrderNodes(JsonElement root, string platform)
    {
        foreach (var path in CandidateArrayPaths(platform))
        {
            if (TryResolvePath(root, path, out var element) && element.ValueKind == JsonValueKind.Array)
            {
                return element.EnumerateArray().ToArray();
            }
        }

        return root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().ToArray()
            : Array.Empty<JsonElement>();
    }

    private static IReadOnlyCollection<string[]> CandidateArrayPaths(string platform)
    {
        return platform.ToLowerInvariant() switch
        {
            "trendyol" => new[]
            {
                new[] { "content" },
                new[] { "orders" },
                new[] { "data", "orders" }
            },
            "hepsiburada" => new[]
            {
                new[] { "data", "items" },
                new[] { "items" },
                new[] { "orders" }
            },
            "n11" => new[]
            {
                new[] { "orders" },
                new[] { "data", "orders" },
                new[] { "result", "orders" }
            },
            _ => new[]
            {
                new[] { "orders" },
                new[] { "items" },
                new[] { "data", "items" }
            }
        };
    }

    private static MarketplaceOrderCandidate? ParseOrder(JsonElement node)
    {
        var addressNode = TryGetObject(node, "shippingAddress") ?? TryGetObject(node, "address") ?? node;
        var orderReference = ReadString(node, "orderNumber", "orderReference", "id", "orderId");
        if (string.IsNullOrWhiteSpace(orderReference))
        {
            return null;
        }

        return new MarketplaceOrderCandidate
        {
            OrderReference = orderReference,
            PackageReference = ReadString(node, "packageNumber", "packageReference", "shipmentPackageId"),
            RecipientName = ReadString(addressNode, "fullName", "name", "receiverName", "customerName") ?? "Marketplace Alici",
            RecipientPhone = ReadString(addressNode, "phone", "phoneNumber", "gsm"),
            RecipientCity = ReadString(addressNode, "city", "cityName") ?? string.Empty,
            RecipientDistrict = ReadString(addressNode, "district", "town", "districtName"),
            RecipientAddress = ReadString(addressNode, "fullAddress", "address", "addressLine1") ?? string.Empty,
            CurrencyCode = ReadString(node, "currencyCode", "currency") ?? "TRY",
            CollectionAmount = ReadDecimal(node, "cashOnDeliveryAmount", "collectionAmount", "totalPrice", "totalAmount"),
            Weight = ReadDecimal(node, "weight", "packageWeight", "cargoWeight") ?? 1,
            Desi = ReadDecimal(node, "desi", "cargoDesi", "volumetricWeight") ?? 1
        };
    }

    private static CargoProviderTypeDto? ResolveProvider(CustomerProfileDto profile, MarketplaceWorkspaceSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.DefaultProvider) &&
            Enum.TryParse<CargoProviderTypeDto>(settings.DefaultProvider, true, out var parsed) &&
            profile.AllowedProviders.Contains(parsed))
        {
            return parsed;
        }

        return profile.AllowedProviders.Count > 0
            ? profile.AllowedProviders[0]
            : null;
    }

    private static AddressInfo BuildSender(CustomerProfileDto profile, MarketplaceWorkspaceSettings settings)
    {
        return new AddressInfo
        {
            Name = settings.SenderName ?? profile.Name,
            CompanyName = profile.Name,
            Phone = settings.SenderPhone,
            City = settings.SenderCity ?? "Istanbul",
            District = settings.SenderDistrict,
            AddressLine1 = settings.SenderAddress ?? "Merkez depo"
        };
    }

    /// <summary>
    /// Sistem-ici shipment referansi uretir; kanal prefix'iyle disambigue eder.
    /// Ornek: "KY-ACME-TY-20260511143022-a1b2c3d4" (Trendyol).
    /// channel=null => "API" prefix'i ile generic (yalnizca kanal cozulemediyse).
    /// </summary>
    private static string GenerateShipmentReference(string tenantKey, OrderSourceChannel? channel)
    {
        var compactTenant = new string(tenantKey.Where(char.IsLetterOrDigit).ToArray());
        if (string.IsNullOrWhiteSpace(compactTenant))
        {
            compactTenant = "tenant";
        }

        var prefix = OrderSourceChannelCodes.GetPrefix(channel);
        var candidate = $"KY-{compactTenant.ToUpperInvariant()}-{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return candidate.Length > 48 ? candidate[..48] : candidate;
    }

    private static bool TryResolvePath(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static JsonElement? TryGetObject(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object
            ? property
            : null;
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String &&
                decimal.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    /// <summary>
    /// Generic <see cref="MarketplaceWorkspaceSettings"/> alanlarini (ApiKey/ApiSecret/StoreId/...)
    /// her bir kanal'in IOrderChannelAdapter implementasyonunun beklediği descriptor field key'lerine map eder.
    /// Amazon (4 ayrı credential) gibi karmasik durumlar map disinda — generic HTTP yoluna duser.
    /// </summary>
    private static (OrderChannelType Channel, IReadOnlyDictionary<string, string> Fields)? TryMapToChannel(
        MarketplaceWorkspaceSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.Platform)) return null;
        var key = s.Platform.Trim().ToLowerInvariant();

        Dictionary<string, string> Build(params (string k, string? v)[] pairs)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in pairs)
                if (!string.IsNullOrWhiteSpace(v)) d[k] = v!;
            return d;
        }

        return key switch
        {
            "trendyol"     => (OrderChannelType.Trendyol,     Build(("supplierId", s.StoreId), ("apiKey", s.ApiKey), ("apiSecret", s.ApiSecret))),
            "hepsiburada"  => (OrderChannelType.Hepsiburada,  Build(("merchantId", s.StoreId), ("username", s.ApiKey), ("password", s.ApiSecret))),
            "n11"          => (OrderChannelType.N11,          Build(("apiKey", s.ApiKey), ("apiSecret", s.ApiSecret))),
            "pazarama"     => (OrderChannelType.Pazarama,     Build(("clientId", s.ApiKey), ("clientSecret", s.ApiSecret))),
            "ciceksepeti"  => (OrderChannelType.CicekSepeti,  Build(("apiKey", s.ApiKey), ("dealerCode", s.StoreId))),
            "cicek sepeti" => (OrderChannelType.CicekSepeti,  Build(("apiKey", s.ApiKey), ("dealerCode", s.StoreId))),
            "pttavm"       => (OrderChannelType.PttAvm,       Build(("apiKey", s.ApiKey), ("sellerId", s.StoreId))),
            "modanisa"     => (OrderChannelType.Modanisa,     Build(("apiKey", s.ApiKey), ("apiSecret", s.ApiSecret), ("sellerId", s.StoreId))),
            // Amazon SP-API icin 4 ayri credential gerekiyor (refreshToken/accessKeyId/secretKey/sellerId);
            // bunlar MarketplaceWorkspaceSettings'te tutulmuyor — OrderChannelMetadata uzerinden /order-channels akisini kullan.
            _ => null
        };
    }

    private static MarketplaceOrderCandidate ConvertRawOrder(RawIncomingOrder raw)
    {
        return new MarketplaceOrderCandidate
        {
            OrderReference = raw.OrderNumber,
            PackageReference = raw.ChannelExternalOrderId,
            RecipientName = string.IsNullOrWhiteSpace(raw.Recipient.FullName) ? "Marketplace Alici" : raw.Recipient.FullName,
            RecipientPhone = raw.Recipient.Phone,
            RecipientCity = raw.Recipient.City ?? string.Empty,
            RecipientDistrict = raw.Recipient.District,
            RecipientAddress = raw.Recipient.AddressLine ?? string.Empty,
            CurrencyCode = string.IsNullOrWhiteSpace(raw.CurrencyCode) ? "TRY" : raw.CurrencyCode,
            CollectionAmount = raw.CollectionAmount,
            Weight = raw.Items.Sum(i => i.WeightKg ?? 0) is var w && w > 0 ? w : 1,
            Desi = 1,
            SourceChannel = raw.SourceChannel
        };
    }

    /// <summary>
    /// Hangi spesifik kanaldan geldigini netlestir.
    /// Live adapter bridge'i icin <paramref name="adapterChannel"/> dolu gelir;
    /// legacy HTTP yolundan gelen siparisler icin <paramref name="platformText"/>
    /// (settings.Platform — "Trendyol", "Hepsiburada", ...) string match'i kullanilir.
    /// </summary>
    private static OrderSourceChannel? ResolveSourceChannel(string? platformText, OrderChannelType? adapterChannel)
    {
        if (adapterChannel.HasValue)
        {
            return (OrderSourceChannel)(int)adapterChannel.Value;
        }
        if (string.IsNullOrWhiteSpace(platformText)) return null;

        return platformText.Trim().ToLowerInvariant() switch
        {
            "trendyol"     => OrderSourceChannel.Trendyol,
            "hepsiburada"  => OrderSourceChannel.Hepsiburada,
            "n11"          => OrderSourceChannel.N11,
            "pazarama"     => OrderSourceChannel.Pazarama,
            "ciceksepeti" or "cicek sepeti" => OrderSourceChannel.CicekSepeti,
            "pttavm"       => OrderSourceChannel.PttAvm,
            "modanisa"     => OrderSourceChannel.Modanisa,
            "amazon"       => OrderSourceChannel.Amazon,
            "gittigidiyor" => OrderSourceChannel.GittiGidiyor,
            "nopcommerce"  => OrderSourceChannel.NopCommerce,
            "shopify"      => OrderSourceChannel.Shopify,
            "woocommerce"  => OrderSourceChannel.WooCommerce,
            "ticimax"      => OrderSourceChannel.Ticimax,
            "ideasoft"     => OrderSourceChannel.IdeaSoft,
            "manual" or "manuel" => OrderSourceChannel.Manual,
            _ => null
        };
    }

    private sealed class MarketplaceOrderCandidate
    {
        public string OrderReference { get; set; } = string.Empty;
        public string? PackageReference { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string? RecipientPhone { get; set; }
        public string RecipientCity { get; set; } = string.Empty;
        public string? RecipientDistrict { get; set; }
        public string RecipientAddress { get; set; } = string.Empty;
        public decimal? CollectionAmount { get; set; }
        public string CurrencyCode { get; set; } = "TRY";
        public decimal Weight { get; set; }
        public decimal Desi { get; set; }
        public OrderChannelType? SourceChannel { get; set; }
    }
}
