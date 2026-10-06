using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class CreateShipmentResponse
{
	public string ShipmentReference { get; set; } = string.Empty;


	public CargoProviderTypeDto Provider { get; set; }

	public ShipmentStatusDto Status { get; set; }

	public string? TrackingNumber { get; set; }

	public string? LabelUrl { get; set; }

	public bool IsIdempotentReplay { get; set; }

	public string? Message { get; set; }
}
