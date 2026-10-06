using System.Diagnostics;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// P5-#6 â€” Provider canli sertifikasyon runner.
///
/// Her saglayici icin uctan uca akisi calistirir:
///   1) Auth/Settings dogrulama  (credential bulunabiliyor mu?)
///   2) CreateShipment           (test gonderi olustur)
///   3) RefreshStatus            (statu sorgula)
///   4) CancelShipment           (test gonderiyi iptal et)
///
/// Her step duration + success/fail + provider message kaydedilir.
/// Sonuc JSON file'da `provider-cert/yyyy-MM-dd/{tenantKey}-{provider}.json` saklanir.
/// </summary>
public sealed class ProviderCertificationRunner
{
    private readonly ICargoProviderResolver _resolver;
    private readonly IProviderSettingsRepository _settings;
    private readonly ILogger<ProviderCertificationRunner> _logger;
    private readonly string _outputRoot;

    public ProviderCertificationRunner(
        ICargoProviderResolver resolver,
        IProviderSettingsRepository settings,
        IWebHostEnvironment env,
        ILogger<ProviderCertificationRunner> logger)
    {
        _resolver = resolver;
        _settings = settings;
        _logger = logger;
        _outputRoot = Path.Combine(env.ContentRootPath, "provider-cert");
        System.IO.Directory.CreateDirectory(_outputRoot);
    }

    public async Task<CertificationReport> RunAsync(
        string tenantKey,
        CargoProviderType provider,
        CertificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CertificationOptions();
        var report = new CertificationReport
        {
            TenantKey = tenantKey,
            Provider = provider.ToString(),
            StartedAtUtc = DateTimeOffset.UtcNow
        };

        // Step 1: Settings + auth check
        var settings = await _settings.GetAsync(tenantKey, provider, cancellationToken).ConfigureAwait(false);
        report.AddStep(new CertificationStep("auth-check", "Settings + credential dogrulama")
        {
            Success = settings is not null && settings.IsEnabled && !string.IsNullOrWhiteSpace(settings.EndpointBase),
            DurationMs = 0,
            Message = settings is null
                ? "Provider settings bulunamadi"
                : !settings.IsEnabled
                    ? "Provider settings devre disi"
                    : string.IsNullOrWhiteSpace(settings.EndpointBase)
                        ? "EndpointBase tanimsiz"
                        : $"Endpoint OK: {settings.EndpointBase}"
        });

        if (!report.Steps[0].Success)
        {
            report.FinishedAtUtc = DateTimeOffset.UtcNow;
            report.OverallSuccess = false;
            await PersistAsync(report, cancellationToken).ConfigureAwait(false);
            return report;
        }

        // Credential settings.SecretJson icinde tasinir; ICargoProvider implementasyonlari
        // ProviderCredential null geldiginde kendi internal lookup'ini yapar.
        ProviderCredential? credential = null;

        var client = _resolver.Resolve(provider);
        var testShipment = BuildTestShipment(tenantKey, provider, options);

        // Step 2: Create
        var createSw = Stopwatch.StartNew();
        ProviderShipmentResult? createResult = null;
        try
        {
            createResult = await client.CreateShipmentAsync(testShipment, credential, cancellationToken).ConfigureAwait(false);
            createSw.Stop();
            report.AddStep(new CertificationStep("create-shipment", "Test gonderi olustur")
            {
                Success = createResult.Success,
                DurationMs = (int)createSw.ElapsedMilliseconds,
                Message = createResult.Success
                    ? $"TrackingNumber={createResult.TrackingNumber ?? "(none)"}"
                    : $"FAIL: {createResult.Message}"
            });
        }
        catch (Exception ex)
        {
            createSw.Stop();
            report.AddStep(new CertificationStep("create-shipment", "Test gonderi olustur")
            {
                Success = false,
                DurationMs = (int)createSw.ElapsedMilliseconds,
                Message = $"EXCEPTION: {ex.Message}"
            });
        }

        // Step 3: RefreshStatus (sadece create basarili ise)
        if (createResult is { Success: true })
        {
            testShipment.TrackingNumber = createResult.TrackingNumber;
            testShipment.ApplyProviderSuccess(ShipmentStatus.InTransit, createResult.TrackingNumber, null, null, createResult.Message);

            var refreshSw = Stopwatch.StartNew();
            try
            {
                var refresh = await client.RefreshStatusAsync(testShipment, credential, cancellationToken).ConfigureAwait(false);
                refreshSw.Stop();
                report.AddStep(new CertificationStep("refresh-status", "Statu sorgula")
                {
                    Success = refresh.Success,
                    DurationMs = (int)refreshSw.ElapsedMilliseconds,
                    Message = refresh.Success
                        ? $"Status={refresh.Status}"
                        : $"FAIL: {refresh.Message}"
                });
            }
            catch (Exception ex)
            {
                refreshSw.Stop();
                report.AddStep(new CertificationStep("refresh-status", "Statu sorgula")
                {
                    Success = false,
                    DurationMs = (int)refreshSw.ElapsedMilliseconds,
                    Message = $"EXCEPTION: {ex.Message}"
                });
            }
        }
        else
        {
            report.AddStep(new CertificationStep("refresh-status", "Statu sorgula")
            {
                Success = false,
                DurationMs = 0,
                Message = "Skipped (create failed)"
            });
        }

        // Step 4: Cancel (cleanup) â€” eger options izin veriyorsa
        if (options.AutoCancelTestShipment && createResult is { Success: true })
        {
            var cancelSw = Stopwatch.StartNew();
            try
            {
                var cancel = await client.CancelShipmentAsync(testShipment, credential, "P5-#6 sertifikasyon test temizligi", cancellationToken).ConfigureAwait(false);
                cancelSw.Stop();
                report.AddStep(new CertificationStep("cancel-shipment", "Test gonderiyi iptal et")
                {
                    Success = cancel.Success,
                    DurationMs = (int)cancelSw.ElapsedMilliseconds,
                    Message = cancel.Success ? "Cancelled" : $"FAIL: {cancel.Message}"
                });
            }
            catch (Exception ex)
            {
                cancelSw.Stop();
                report.AddStep(new CertificationStep("cancel-shipment", "Test gonderiyi iptal et")
                {
                    Success = false,
                    DurationMs = (int)cancelSw.ElapsedMilliseconds,
                    Message = $"EXCEPTION: {ex.Message}"
                });
            }
        }

        report.FinishedAtUtc = DateTimeOffset.UtcNow;
        report.OverallSuccess = report.Steps.All(s => s.Success);
        await PersistAsync(report, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Certification run: {Tenant}/{Provider} OverallSuccess={Ok} StepCount={Count}",
            tenantKey, provider, report.OverallSuccess, report.Steps.Count);

        return report;
    }

    private async Task PersistAsync(CertificationReport report, CancellationToken ct)
    {
        var dayDir = Path.Combine(_outputRoot, report.StartedAtUtc.ToString("yyyy-MM-dd"));
        System.IO.Directory.CreateDirectory(dayDir);
        var fileName = $"{report.TenantKey}-{report.Provider}-{report.StartedAtUtc:HHmmss}.json";
        var path = Path.Combine(dayDir, fileName);
        var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    private static CargoShipment BuildTestShipment(string tenantKey, CargoProviderType provider, CertificationOptions options)
    {
        var sender = new AddressInfo
        {
            Name = "Kargoyeri Test Gonderici",
            CompanyName = "Kargoyeri",
            Phone = "05550000001",
            Email = "test@kargoyeri.com",
            City = options.SenderCity,
            District = options.SenderDistrict,
            AddressLine1 = "Test Sokak No:1",
            CountryCode = "TR"
        };
        var recipient = new AddressInfo
        {
            Name = "Kargoyeri Test Alici",
            Phone = "05550000002",
            Email = "test-recipient@kargoyeri.com",
            City = options.RecipientCity,
            District = options.RecipientDistrict,
            AddressLine1 = "Sertifikasyon Test Caddesi No:1",
            CountryCode = "TR"
        };
        var pkg = new PackageInfo
        {
            PackageSequence = 1,
            Weight = 1.0m,
            Desi = 1.0m,
            Description = $"P5-#6 sertifikasyon testi ({provider})"
        };
        return CargoShipment.Create(
            shipmentReference: $"CERT-{Guid.NewGuid():N}".Substring(0, 16).ToUpperInvariant(),
            tenantKey: tenantKey,
            orderReference: $"CERT-ORDER-{DateTime.UtcNow:yyyyMMddHHmmss}",
            clientShipmentReference: null,
            idempotencyKey: $"cert-{Guid.NewGuid():N}",
            provider: provider,
            source: IntegrationSourceType.Manual,
            collectionAmount: null,
            currencyCode: "TRY",
            sender: sender,
            recipient: recipient,
            packages: new[] { pkg },
            metadata: new Dictionary<string, string> { ["cert"] = "true" },
            // Sertifikasyon koşusu — kanal etiketi Custom; kod "cert" ile
            // gerçek müşteri trafiğinden filtrelenebilir.
            sourceChannel: OrderSourceChannel.Custom,
            sourceChannelCode: "cert");
    }

    public IReadOnlyList<CertificationReport> ListRecent(int days = 7)
    {
        var reports = new List<CertificationReport>();
        if (!System.IO.Directory.Exists(_outputRoot)) return reports;

        var cutoff = DateTime.UtcNow.AddDays(-days).Date;
        foreach (var dir in System.IO.Directory.EnumerateDirectories(_outputRoot))
        {
            if (!DateTime.TryParse(Path.GetFileName(dir), out var dirDate) || dirDate < cutoff) continue;
            foreach (var file in System.IO.Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var report = System.Text.Json.JsonSerializer.Deserialize<CertificationReport>(json);
                    if (report is not null) reports.Add(report);
                }
                catch { /* corrupt file ignore */ }
            }
        }
        return reports.OrderByDescending(r => r.StartedAtUtc).ToList();
    }
}

public sealed class CertificationOptions
{
    public bool AutoCancelTestShipment { get; set; } = true;
    public string SenderCity { get; set; } = "Istanbul";
    public string SenderDistrict { get; set; } = "Kadikoy";
    public string RecipientCity { get; set; } = "Ankara";
    public string RecipientDistrict { get; set; } = "Cankaya";
}

public sealed class CertificationReport
{
    public string TenantKey { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset FinishedAtUtc { get; set; }
    public bool OverallSuccess { get; set; }
    public List<CertificationStep> Steps { get; set; } = new();

    public void AddStep(CertificationStep step) => Steps.Add(step);
    public int TotalDurationMs => Steps.Sum(s => s.DurationMs);
}

public sealed class CertificationStep
{
    public string Code { get; set; }
    public string Description { get; set; }
    public bool Success { get; set; }
    public int DurationMs { get; set; }
    public string? Message { get; set; }

    public CertificationStep() { Code = string.Empty; Description = string.Empty; }
    public CertificationStep(string code, string description)
    {
        Code = code;
        Description = description;
    }
}

