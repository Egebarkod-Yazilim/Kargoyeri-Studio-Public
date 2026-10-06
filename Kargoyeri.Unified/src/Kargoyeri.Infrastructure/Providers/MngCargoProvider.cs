using Kargoyeri.Contracts.Providers;
using Kargoyeri.Contracts.Shipments;

namespace Kargoyeri.Infrastructure.Providers;

/// <summary>
/// MNG Kargo sağlayıcısı.
/// Faz 1'de stub; Faz 2'de gerçek MNG API'sine bağlanır.
/// </summary>
public sealed class MngCargoProvider : ICargoProvider
{
    private readonly HttpClient _http;

    public MngCargoProvider(HttpClient http) => _http = http;

    public string Id          => "mng";
    public string DisplayName => "MNG Kargo";
    public string? Description => "MNG Kargo entegrasyonu (Faz 2 — yakında)";
    public string? LogoUrl    => "/img/providers/mng.svg";

    public ProviderCapability Capabilities =>
        ProviderCapability.CreateShipment |
        ProviderCapability.TrackShipment  |
        ProviderCapability.PrintBarcode   |
        ProviderCapability.CancelShipment;

    public ProviderFormSchema FormSchema => new(
        ProviderId: Id,
        Title: "MNG Kargo Bağlantısı",
        Description: "MNG Kargo müşteri panelinizden alacağınız bilgileri girin.",
        DocumentationUrl: "https://www.mngkargo.com.tr/entegrasyon",
        Fields: new[]
        {
            new ProviderCredentialField(
                "customer_number",
                "Müşteri Numarası",
                CredentialFieldType.Text,
                Placeholder: "123456",
                HelpText: "MNG panelinizdeki 6 haneli müşteri numaranız."),

            new ProviderCredentialField(
                "username",
                "Kullanıcı Adı",
                CredentialFieldType.Text,
                Placeholder: "api_user"),

            new ProviderCredentialField(
                "password",
                "Şifre",
                CredentialFieldType.Password,
                Placeholder: "••••••••"),

            new ProviderCredentialField(
                "environment",
                "Ortam",
                CredentialFieldType.Select,
                Required: true,
                DefaultValue: "test",
                Options: new[]
                {
                    new SelectOption("test",       "Test (Sandbox)"),
                    new SelectOption("production", "Canlı (Production)"),
                }),
        }
    );

    public Task<CredentialValidationResult> ValidateCredentialsAsync(
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        // TODO Faz 2: MNG kimlik doğrulama endpoint'ine istek at
        throw new NotImplementedException("MNG transport Faz 2'de implement edilecek.");
    }

    public Task<ShipmentResult> CreateShipmentAsync(
        ShipmentRequest request,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("MNG transport Faz 2'de implement edilecek.");
    }

    public Task<TrackingResult> TrackShipmentAsync(
        string trackingNumber,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("MNG transport Faz 2'de implement edilecek.");
    }

    public Task<CancelResult> CancelShipmentAsync(
        string shipmentId,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("MNG transport Faz 2'de implement edilecek.");
    }

    public Task<BarcodeResult> GetBarcodeAsync(
        string shipmentId,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("MNG transport Faz 2'de implement edilecek.");
    }

    public Task<RateResult> GetRatesAsync(
        RateRequest request,
        Dictionary<string, string> credentials,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("MNG transport Faz 2'de implement edilecek.");
    }
}
