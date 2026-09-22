// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class CreateShipmentResponse
{
    public string ShipmentReference { get; set; }
    public global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto Provider { get; set; }
    public global::Kargoyeri.Contracts.Enums.ShipmentStatusDto Status { get; set; }
    public string TrackingNumber { get; set; }
    public string LabelUrl { get; set; }
    public bool IsIdempotentReplay { get; set; }
    public string Message { get; set; }
    public CreateShipmentResponse() { }
}
