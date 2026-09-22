namespace Kargoyeri.Contracts.Dtos;

public sealed class CancelShipmentRequest
{
	public string? TenantKey { get; set; }

	public string? Reason { get; set; }
}
