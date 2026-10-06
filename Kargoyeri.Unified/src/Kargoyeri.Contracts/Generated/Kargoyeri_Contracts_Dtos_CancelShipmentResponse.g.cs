// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class CancelShipmentResponse
{
    public string ShipmentReference { get; set; }
    public global::Kargoyeri.Contracts.Enums.ShipmentStatusDto Status { get; set; }
    public string Message { get; set; }
    public CancelShipmentResponse() { }
}
