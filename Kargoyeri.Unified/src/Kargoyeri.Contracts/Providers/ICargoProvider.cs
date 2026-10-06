using Kargoyeri.Contracts.Shipments;

namespace Kargoyeri.Contracts.Providers;

/// <summary>
/// Tüm kargo sağlayıcılarının implement etmesi gereken temel arayüz.
/// Her provider kendi capabilities setini ve credential şemasını tanımlar.
/// </summary>
public interface ICargoProvider
{
    /// <summary>Benzersiz provider kimliği (ör: "mng", "ups", "aras")</summary>
    string Id { get; }

    /// <summary>Kullanıcıya gösterilecek ad</summary>
    string DisplayName { get; }

    /// <summary>Kısa açıklama</summary>
    string? Description { get; }

    /// <summary>Logo URL (wwwroot veya CDN)</summary>
    string? LogoUrl { get; }

    /// <summary>Bu sağlayıcının desteklediği işlemler — UI buna göre şekillenir</summary>
    ProviderCapability Capabilities { get; }

    /// <summary>Bağlantı formu şeması — UI dinamik olarak render eder</summary>
    ProviderFormSchema FormSchema { get; }

    // ─── Credential ───────────────────────────────────────────────────────────

    /// <summary>Girilen kimlik bilgilerini doğrular (test bağlantısı)</summary>
    Task<CredentialValidationResult> ValidateCredentialsAsync(
        Dictionary<string, string> credentials,
        CancellationToken ct = default);

    // ─── Shipment ─────────────────────────────────────────────────────────────

    /// <summary>Yeni gönderi oluşturur. Capability: CreateShipment</summary>
    Task<ShipmentResult> CreateShipmentAsync(
        ShipmentRequest request,
        Dictionary<string, string> credentials,
        CancellationToken ct = default);

    /// <summary>Gönderi takibi. Capability: TrackShipment</summary>
    Task<TrackingResult> TrackShipmentAsync(
        string trackingNumber,
        Dictionary<string, string> credentials,
        CancellationToken ct = default);

    /// <summary>Gönderi iptali. Capability: CancelShipment</summary>
    Task<CancelResult> CancelShipmentAsync(
        string shipmentId,
        Dictionary<string, string> credentials,
        CancellationToken ct = default);

    /// <summary>Barkod / kargo etiketi. Capability: PrintBarcode</summary>
    Task<BarcodeResult> GetBarcodeAsync(
        string shipmentId,
        Dictionary<string, string> credentials,
        CancellationToken ct = default);

    /// <summary>Fiyat sorgulama. Capability: GetRates</summary>
    Task<RateResult> GetRatesAsync(
        RateRequest request,
        Dictionary<string, string> credentials,
        CancellationToken ct = default);
}

/// <summary>Credential doğrulama sonucu</summary>
public record CredentialValidationResult(
    bool Success,
    string? Message = null,
    string? AccountName = null
);
