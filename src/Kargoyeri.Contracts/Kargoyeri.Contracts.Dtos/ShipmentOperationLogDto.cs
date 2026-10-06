using System;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ShipmentOperationLogDto
{
	public string ShipmentReference { get; set; } = string.Empty;


	public string Operation { get; set; } = string.Empty;


	public string Severity { get; set; } = string.Empty;


	public string Message { get; set; } = string.Empty;


	public string? ProviderPayload { get; set; }

	public DateTimeOffset OccurredAtUtc { get; set; }
}
