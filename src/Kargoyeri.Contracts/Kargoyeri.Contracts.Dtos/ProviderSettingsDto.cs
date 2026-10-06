using System;
using System.Collections.Generic;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ProviderSettingsDto
{
	public CargoProviderTypeDto Provider { get; set; }

	public bool IsEnabled { get; set; }

	public string? ClientCode { get; set; }

	public string? Username { get; set; }

	public string? PasswordMasked { get; set; }

	public string? ApiKeyMasked { get; set; }

	public string? EndpointBase { get; set; }

	public Dictionary<string, string> AdditionalSettings { get; set; } = new Dictionary<string, string>();


	public DateTimeOffset UpdatedAtUtc { get; set; }
}
