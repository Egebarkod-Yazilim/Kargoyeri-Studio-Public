// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ShipmentDetailResponse
{
    public string ShipmentReference { get; set; }
    public string CustomerCode { get; set; }
    public string OrderReference { get; set; }
    public string ClientShipmentReference { get; set; }
    public string IdempotencyKey { get; set; }
    public global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto Provider { get; set; }
    public global::Kargoyeri.Contracts.Enums.IntegrationSourceTypeDto Source { get; set; }
    public global::Kargoyeri.Contracts.Enums.ShipmentStatusDto Status { get; set; }
    public string TrackingNumber { get; set; }
    public string LabelUrl { get; set; }
    public string ProviderMessage { get; set; }
    public string ErrorMessage { get; set; }
    public decimal? CollectionAmount { get; set; }
    public string CurrencyCode { get; set; }
    public global::Kargoyeri.Contracts.Dtos.AddressDto Sender { get; set; }
    public global::Kargoyeri.Contracts.Dtos.AddressDto Recipient { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Contracts.Dtos.PackageDto> Packages { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> Metadata { get; set; }
    public global::System.DateTimeOffset CreatedAtUtc { get; set; }
    public global::System.DateTimeOffset UpdatedAtUtc { get; set; }
    public global::System.DateTimeOffset? LastStatusCheckAtUtc { get; set; }
    public int RetryCount { get; set; }
    public ShipmentDetailResponse() { }
}
