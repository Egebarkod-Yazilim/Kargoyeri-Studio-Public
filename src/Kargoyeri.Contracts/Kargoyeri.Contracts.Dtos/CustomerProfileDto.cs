using System;
using System.Collections.Generic;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class CustomerProfileDto
{
	public string TenantKey { get; set; } = string.Empty;


	public string Name { get; set; } = string.Empty;


	public bool IsActive { get; set; }

	public List<CargoProviderTypeDto> AllowedProviders { get; set; } = new List<CargoProviderTypeDto>();


	public List<NotificationTargetDto> NotificationTargets { get; set; } = new List<NotificationTargetDto>();


	public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();


	public DateTimeOffset UpdatedAtUtc { get; set; }
}
