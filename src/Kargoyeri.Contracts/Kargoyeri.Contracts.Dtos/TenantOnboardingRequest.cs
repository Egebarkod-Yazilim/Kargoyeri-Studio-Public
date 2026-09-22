using System.Collections.Generic;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class TenantOnboardingRequest
{
	public string? TenantKey { get; set; }

	public string Name { get; set; } = string.Empty;


	public bool IsActive { get; set; } = true;


	public List<CargoProviderTypeDto> AllowedProviders { get; set; } = new List<CargoProviderTypeDto>();


	public List<NotificationTargetDto> NotificationTargets { get; set; } = new List<NotificationTargetDto>();


	public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

}
