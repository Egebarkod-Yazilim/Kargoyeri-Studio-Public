using System;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ShipmentListItemResponse
{
	public string ShipmentReference { get; set; } = string.Empty;


	public string CustomerCode { get; set; } = string.Empty;


	public string OrderReference { get; set; } = string.Empty;


	public CargoProviderTypeDto Provider { get; set; }

	public ShipmentStatusDto Status { get; set; }

	public string? TrackingNumber { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }

	public OrderSourceChannelDto? SourceChannel { get; set; }

	public string? SourceChannelCode { get; set; }
}
