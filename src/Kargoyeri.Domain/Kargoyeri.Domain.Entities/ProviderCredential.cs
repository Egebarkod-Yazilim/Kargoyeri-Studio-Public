using System;
using System.Collections.Generic;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Domain.Entities;

public sealed class ProviderCredential
{
	public string TenantKey { get; set; } = string.Empty;


	public CargoProviderType Provider { get; set; }

	public bool IsEnabled { get; set; }

	public string? ClientCode { get; set; }

	public string? Username { get; set; }

	public string? Password { get; set; }

	public string? ApiKey { get; set; }

	public string? EndpointBase { get; set; }

	public Dictionary<string, string> AdditionalSettings { get; set; } = new Dictionary<string, string>();


	public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

}
