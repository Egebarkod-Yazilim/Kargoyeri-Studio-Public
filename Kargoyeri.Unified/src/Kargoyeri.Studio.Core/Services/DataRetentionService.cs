using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Studio.Core.Infrastructure;

namespace Kargoyeri.Studio.Core.Services;

/// <summary>
/// KVKK + DATA-RETENTION.md politikasini uygulayan otomatik temizleme job'u (P1-#3).
///
/// Calisma kosulu:  Gunde 1 kez (24 saat).
/// Yaptiklari:
///   1) 24 aydan eski gonderilerin alici bilgilerini anonimlestirir
///      (Recipient.Name/Phone/Email/AddressLine* — VUK gerekli alanlar
///       OrderReference / CollectionAmount / TrackingNumber korunur).
///   2) KVKK silme talebi onaylanmis ve HardDeleteAt gecmis tenant'larin
///      tum gonderilerini (yas farketmeksizin) anonimlestirir + tenant
///      Metadata'sinda kvkk.deletion.status=executed isaretler.
///
/// Idempotent: shipment.Metadata["kvkk.anonymized"]=true olanlar atlanir.
/// </summary>
internal sealed class DataRetentionService : BackgroundService
{
    /// <summary>Gonderiler bu yastan eski olunca otomatik anonimlestirilir.</summary>
    public const int ShipmentRetentionMonths = 24;

    /// <summary>shipment.Metadata bayragi.</summary>
    public const string AnonymizedFlagKey = "kvkk.anonymized";

    /// <summary>Tenant Metadata — KVKK talebi tamamen islendigini isaretler.</summary>
    public const string DeletionExecutedStatus = "executed";
    public const string KeyExecutedAt = "kvkk.deletion.executedAt";

    private const string AnonName    = "ANONIMLESTIRILMIS";
    private const string AnonAddress = "[ANONIM]";

    /// <summary>Her dongu arasi 24 saat (uretimde sabit; testte degistirilemez — basit P1).</summary>
    private static readonly TimeSpan CycleInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(IServiceScopeFactory scopeFactory, ILogger<DataRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DataRetentionService baslatildi. Politika: gonderiler {Months} ay, KVKK silme {Grace} gun.",
            ShipmentRetentionMonths, KvkkDeletionRequest.GracePeriodDays);

        // İlk çalışmayı 60 saniye geciktir — uygulama tam ayağa kalksın
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataRetention dongusu hata. 1 saat sonra yeniden denenecek.");
                try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try { await Task.Delay(CycleInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("DataRetentionService durduruldu.");
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var customerSvc  = scope.ServiceProvider.GetRequiredService<CustomerService>();
        var shipmentRepo = scope.ServiceProvider.GetRequiredService<IShipmentRepository>();

        var tenants = await customerSvc.ListAllAsync(ct);
        var ageCutoff = DateTimeOffset.UtcNow.AddMonths(-ShipmentRetentionMonths);

        var totalAnonymized   = 0;
        var totalKvkkExecuted = 0;

        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();

            var deletion = KvkkDeletionRequest.Read(tenant.Metadata);
            var kvkkDue  = deletion.IsApproved
                           && deletion.HardDeleteAt is { } hd
                           && hd <= DateTimeOffset.UtcNow
                           && !string.Equals(deletion.Status, DeletionExecutedStatus, StringComparison.OrdinalIgnoreCase);

            // Tenant'in gonderilerini liste ile cek
            IReadOnlyCollection<CargoShipment> shipments;
            try
            {
                shipments = await shipmentRepo.ListAsync(tenant.TenantKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Tenant {Tenant} icin gonderi listesi okunamadi — atlandi.", tenant.TenantKey);
                continue;
            }

            var anonymizedThisTenant = 0;
            foreach (var s in shipments)
            {
                ct.ThrowIfCancellationRequested();

                if (s.Metadata.TryGetValue(AnonymizedFlagKey, out var flag) &&
                    string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var oldEnough = s.CreatedAtUtc < ageCutoff;
                if (!oldEnough && !kvkkDue) continue;

                AnonymizeShipment(s);
                try
                {
                    await shipmentRepo.UpsertAsync(s, ct);
                    anonymizedThisTenant++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Anonimlestirme yazma hatasi: tenant={Tenant} ref={Ref}",
                        tenant.TenantKey, s.ShipmentReference);
                }
            }

            if (anonymizedThisTenant > 0)
            {
                totalAnonymized += anonymizedThisTenant;
                _logger.LogInformation("DataRetention: tenant {Tenant} — {Count} gonderi anonimlestirildi.",
                    tenant.TenantKey, anonymizedThisTenant);
            }

            // KVKK silme talebi tamamlandi olarak isaretlenir
            if (kvkkDue)
            {
                var meta = new Dictionary<string, string>(tenant.Metadata, StringComparer.OrdinalIgnoreCase)
                {
                    [KvkkDeletionRequest.KeyStatus] = DeletionExecutedStatus,
                    [KeyExecutedAt] = DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture)
                };

                await customerSvc.UpsertAsync(tenant.TenantKey, new UpsertCustomerRequest
                {
                    TenantKey           = tenant.TenantKey,
                    Name                = tenant.Name,
                    IsActive            = false,
                    AllowedProviders    = tenant.AllowedProviders.ToList(),
                    NotificationTargets = tenant.NotificationTargets.ToList(),
                    Metadata            = meta
                }, ct);

                totalKvkkExecuted++;
                _logger.LogWarning("KVKK hard-delete tamamlandi: tenant={Tenant} requestedBy={User} requestedAt={Req}",
                    tenant.TenantKey, deletion.RequestedBy, deletion.RequestedAt);
            }
        }

        if (totalAnonymized == 0 && totalKvkkExecuted == 0)
            _logger.LogInformation("DataRetention dongusu: islem yapilmasi gereken kayit yok ({TenantCount} tenant tarandi).", tenants.Count);
        else
            _logger.LogInformation("DataRetention dongusu tamamlandi: {Anon} gonderi anonimlestirildi, {Kvkk} KVKK talebi executed.",
                totalAnonymized, totalKvkkExecuted);
    }

    private static void AnonymizeShipment(CargoShipment s)
    {
        // Recipient (alici) — kisisel veri
        if (s.Recipient is not null)
        {
            s.Recipient.Name         = AnonName;
            s.Recipient.CompanyName  = null;
            s.Recipient.Phone        = null;
            s.Recipient.Email        = null;
            s.Recipient.AddressLine1 = AnonAddress;
            s.Recipient.AddressLine2 = null;
            s.Recipient.District     = null;
            s.Recipient.PostalCode   = null;
            // City korunur (raporlama / istatistik icin yeterli — kisi tanimlamaz)
        }

        // Sender — kurumsal veri ama yine de telefon/email gibi alanlari nullify
        if (s.Sender is not null)
        {
            s.Sender.Phone = null;
            s.Sender.Email = null;
        }

        // OrderReference, CollectionAmount, TrackingNumber, CreatedAtUtc, Provider, Status korunur — VUK + audit.
        s.Metadata[AnonymizedFlagKey] = "true";
        s.Metadata["kvkk.anonymizedAt"] =
            DateTimeOffset.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        s.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
