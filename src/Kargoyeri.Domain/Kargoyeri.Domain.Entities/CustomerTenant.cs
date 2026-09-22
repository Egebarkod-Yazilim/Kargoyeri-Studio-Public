using System;
using System.Collections.Generic;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Domain.Entities;

public sealed class CustomerTenant
{
	public string TenantKey { get; set; } = string.Empty;


	public string Name { get; set; } = string.Empty;


	public bool IsActive { get; set; } = true;


	public string ApiKeyHash { get; set; } = string.Empty;


	public List<CargoProviderType> AllowedProviders { get; set; } = new List<CargoProviderType>();


	public List<NotificationTarget> NotificationTargets { get; set; } = new List<NotificationTarget>();


	public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();


	public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;


	public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;


	public bool CanUseProvider(CargoProviderType provider)
	{
		return AllowedProviders.Count == 0 || AllowedProviders.Contains(provider);
	}
}
