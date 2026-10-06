using System;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class NotificationMessageDto
{
	public Guid Id { get; set; }

	public string CustomerCode { get; set; } = string.Empty;


	public string ShipmentReference { get; set; } = string.Empty;


	public NotificationEventTypeDto EventType { get; set; }

	public NotificationChannelDto Channel { get; set; }

	public NotificationDeliveryStatusDto Status { get; set; }

	public string Address { get; set; } = string.Empty;


	public string Subject { get; set; } = string.Empty;


	public string Body { get; set; } = string.Empty;


	public string? ErrorMessage { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset? DeliveredAtUtc { get; set; }
}
