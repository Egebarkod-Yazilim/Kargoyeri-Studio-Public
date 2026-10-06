using Kargoyeri.Contracts.Providers;
using Kargoyeri.Contracts.Shipments;

namespace Kargoyeri.Infrastructure.Providers;

/// <summary>
/// Demo ve geliştirme için tam özellikli sahte sağlayıcı.
/// Gerçek HTTP çağrısı yapmaz; gerçekçi cevaplar üretir.
/// Credential olarak her şeyi kabul eder (test kolaylığı).
/// </summary>
public sealed class MockCargoProvider : ICargoProvider
{
    public string Id          => "mock";
    public string DisplayName => "Demo Kargo (Mock)";
    public string? Description => "Geliştirme ve test için. Gerçek istek atmaz.";
    public string? LogoUrl    => null;

    public ProviderCapability Capabilities =>
        ProviderCapability.Full;

    public ProviderFormSchema FormSchema => new(
        ProviderId: Id,
        Title: "Demo Kargo Bağlantısı",
        Description: "Herhangi bir değer girebilirsiniz — bağlantı her zaman başarılı olur.",
        Fields: new[]
        {
            new ProviderCredentialField("api_key",  "API Anahtarı", CredentialFieldType.Password,
                Placeholder: "demo-api-key-xxx",
                HelpText: "Gerçek sistemde sağlayıcı panelinden alınır."),
            new ProviderCredentialField("base_url", "API URL", CredentialFieldType.Url,
                Placeholder: "https://api.demokargo.com/v1",
                Required: false),
        }
    );

    public Task<CredentialValidationResult> ValidateCredentialsAsync(
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        return Task.FromResult(new CredentialValidationResult(
            Success: true,
            Message: "Bağlantı başarılı.",
            AccountName: "Demo Hesabı"
        ));
    }

    public Task<ShipmentResult> CreateShipmentAsync(
        ShipmentRequest request,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        var id = $"MOCK-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        return Task.FromResult(new ShipmentResult(
            Success: true,
            ShipmentId: id,
            TrackingNumber: $"TRK{id[5..]}",
            BarcodeBase64: GenerateFakeBarcode()
        ));
    }

    public Task<TrackingResult> TrackShipmentAsync(
        string trackingNumber,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new TrackingResult(
            Success: true,
            TrackingNumber: trackingNumber,
            Status: ShipmentStatus.InTransit,
            StatusLabel: "Yolda",
            Events: new[]
            {
                new TrackingEvent(now.AddHours(-5),  "Gönderi oluşturuldu",           "İstanbul Depo"),
                new TrackingEvent(now.AddHours(-3),  "Kurye tarafından teslim alındı", "İstanbul"),
                new TrackingEvent(now.AddHours(-1),  "Transfer merkezine ulaştı",      "Ankara Transfer"),
            }
        ));
    }

    public Task<CancelResult> CancelShipmentAsync(
        string shipmentId,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        return Task.FromResult(new CancelResult(Success: true));
    }

    public Task<BarcodeResult> GetBarcodeAsync(
        string shipmentId,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        return Task.FromResult(new BarcodeResult(
            Success: true,
            BarcodeBase64: GenerateFakeBarcode(),
            Format: "PNG"
        ));
    }

    public Task<RateResult> GetRatesAsync(
        RateRequest request,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        return Task.FromResult(new RateResult(
            Success: true,
            Options: new[]
            {
                new RateOption("Ekonomik",  29.90m, "TRY", 3),
                new RateOption("Standart",  49.90m, "TRY", 2),
                new RateOption("Ekspres",   89.90m, "TRY", 1),
            }
        ));
    }

    // Sahte 1px PNG base64 (barkod placeholder)
    private static string GenerateFakeBarcode() =>
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
}
