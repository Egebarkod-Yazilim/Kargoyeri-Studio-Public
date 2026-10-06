namespace Kargoyeri.Contracts.Shipments;

// ─── Request Models ────────────────────────────────────────────────────────────

public record ShipmentRequest(
    string ProviderId,
    Address Sender,
    Address Recipient,
    Package Package,
    string? Reference = null,
    string? Notes = null
);

public record Address(
    string FullName,
    string Phone,
    string City,
    string District,
    string AddressLine,
    string PostalCode = "",
    string Country = "TR"
);

public record Package(
    decimal WeightKg,
    decimal? WidthCm = null,
    decimal? HeightCm = null,
    decimal? DepthCm = null,
    string? ContentDescription = null
);

public record RateRequest(
    string ProviderId,
    Address From,
    Address To,
    Package Package
);

// ─── Result Models ─────────────────────────────────────────────────────────────

public record ShipmentResult(
    bool Success,
    string? ShipmentId,
    string? TrackingNumber,
    string? BarcodeBase64,
    string? ErrorMessage = null
);

public record TrackingResult(
    bool Success,
    string TrackingNumber,
    ShipmentStatus Status,
    string StatusLabel,
    IReadOnlyList<TrackingEvent> Events,
    string? ErrorMessage = null
);

public record TrackingEvent(
    DateTimeOffset OccurredAt,
    string Description,
    string? Location
);

public record CancelResult(
    bool Success,
    string? ErrorMessage = null
);

public record BarcodeResult(
    bool Success,
    string? BarcodeBase64,
    string? Format,   // "PDF", "PNG", "ZPL"
    string? ErrorMessage = null
);

public record RateResult(
    bool Success,
    IReadOnlyList<RateOption> Options,
    string? ErrorMessage = null
);

public record RateOption(
    string ServiceName,
    decimal Price,
    string Currency,
    int EstimatedDays
);

// ─── Enums ─────────────────────────────────────────────────────────────────────

public enum ShipmentStatus
{
    Unknown,
    Created,
    PickedUp,
    InTransit,
    OutForDelivery,
    Delivered,
    Returned,
    Cancelled,
    Failed,
}

public static class ShipmentStatusExtensions
{
    public static string ToLabel(this ShipmentStatus s) => s switch
    {
        ShipmentStatus.Created         => "Oluşturuldu",
        ShipmentStatus.PickedUp        => "Teslim Alındı",
        ShipmentStatus.InTransit       => "Yolda",
        ShipmentStatus.OutForDelivery  => "Dağıtımda",
        ShipmentStatus.Delivered       => "Teslim Edildi",
        ShipmentStatus.Returned        => "İade",
        ShipmentStatus.Cancelled       => "İptal",
        ShipmentStatus.Failed          => "Başarısız",
        _                              => "Bilinmiyor",
    };

    public static string ToBadgeClass(this ShipmentStatus s) => s switch
    {
        ShipmentStatus.Delivered       => "badge-success",
        ShipmentStatus.Cancelled       => "badge-danger",
        ShipmentStatus.Failed          => "badge-danger",
        ShipmentStatus.InTransit       => "badge-info",
        ShipmentStatus.OutForDelivery  => "badge-warning",
        _                              => "badge-neutral",
    };
}
