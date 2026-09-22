using System;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Domain.Entities;

public sealed class NotificationMessage
{
	public Guid Id { get; set; } = Guid.NewGuid();


	public string TenantKey { get; set; } = string.Empty;


	public string ShipmentReference { get; set; } = string.Empty;


	public NotificationEventType EventType { get; set; }

	public NotificationChannel Channel { get; set; }

	public NotificationDeliveryStatus Status { get; set; } = NotificationDeliveryStatus.Queued;


	public string Address { get; set; } = string.Empty;


	public string Subject { get; set; } = string.Empty;


	public string Body { get; set; } = string.Empty;


	public string? ErrorMessage { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;


	public DateTimeOffset? DeliveredAtUtc { get; set; }

	public void MarkDelivered()
	{
		Status = NotificationDeliveryStatus.Delivered;
		DeliveredAtUtc = DateTimeOffset.UtcNow;
		ErrorMessage = null;
	}

	public void MarkFailed(string? error)
	{
		Status = NotificationDeliveryStatus.Failed;
		ErrorMessage = error;
		DeliveredAtUtc = null;
	}

	public void MarkIgnored(string? message = null)
	{
		Status = NotificationDeliveryStatus.Ignored;
		ErrorMessage = message;
		DeliveredAtUtc = null;
	}
}
