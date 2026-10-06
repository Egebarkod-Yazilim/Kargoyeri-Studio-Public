using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Application.Abstractions.Providers;

public sealed class ProviderShipmentResult
{
	public bool Success { get; init; }

	public ShipmentStatus Status { get; init; }

	public string? TrackingNumber { get; init; }

	public string? LabelUrl { get; init; }

	public string? LabelContentBase64 { get; init; }

	public string? Message { get; init; }

	public string? RawResponse { get; init; }

	public static ProviderShipmentResult Ok(ShipmentStatus status, string? trackingNumber, string? labelUrl, string? labelContentBase64, string? message, string? rawResponse = null)
	{
		return new ProviderShipmentResult
		{
			Success = true,
			Status = status,
			TrackingNumber = trackingNumber,
			LabelUrl = labelUrl,
			LabelContentBase64 = labelContentBase64,
			Message = message,
			RawResponse = rawResponse
		};
	}

	public static ProviderShipmentResult Fail(string message, string? rawResponse = null)
	{
		return new ProviderShipmentResult
		{
			Success = false,
			Status = ShipmentStatus.Failed,
			Message = message,
			RawResponse = rawResponse
		};
	}
}
