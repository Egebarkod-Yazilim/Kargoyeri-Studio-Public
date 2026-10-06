using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class CancelShipmentResponse
{
	public string ShipmentReference { get; set; } = string.Empty;


	public ShipmentStatusDto Status { get; set; }

	public string Message { get; set; } = string.Empty;

}
