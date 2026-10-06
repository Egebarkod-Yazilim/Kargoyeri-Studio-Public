// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ShipmentListItemResponse
{
    public string ShipmentReference { get; set; }
    public string CustomerCode { get; set; }
    public string OrderReference { get; set; }
    public global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto Provider { get; set; }
    public global::Kargoyeri.Contracts.Enums.ShipmentStatusDto Status { get; set; }
    public string TrackingNumber { get; set; }
    public global::System.DateTimeOffset CreatedAtUtc { get; set; }
    public global::System.DateTimeOffset UpdatedAtUtc { get; set; }
    public global::Kargoyeri.Contracts.Enums.OrderSourceChannelDto? SourceChannel { get; set; }
    public string SourceChannelCode { get; set; }
    public ShipmentListItemResponse() { }
}
