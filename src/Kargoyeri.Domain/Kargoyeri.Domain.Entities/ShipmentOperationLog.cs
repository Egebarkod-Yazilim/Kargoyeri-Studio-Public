using System;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Domain.Entities;

public sealed class ShipmentOperationLog
{
	public Guid Id { get; set; } = Guid.NewGuid();


	public string TenantKey { get; set; } = string.Empty;


	public string ShipmentReference { get; set; } = string.Empty;


	public CargoOperationType Operation { get; set; }

	public LogSeverity Severity { get; set; }

	public string Message { get; set; } = string.Empty;


	public string? ProviderPayload { get; set; }

	public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;

}
