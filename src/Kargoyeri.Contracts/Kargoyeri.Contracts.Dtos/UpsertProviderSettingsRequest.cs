using System.Collections.Generic;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class UpsertProviderSettingsRequest
{
	public CargoProviderTypeDto Provider { get; set; }

	public bool IsEnabled { get; set; }

	public string? ClientCode { get; set; }

	public string? Username { get; set; }

	public string? Password { get; set; }

	public string? ApiKey { get; set; }

	public string? EndpointBase { get; set; }

	public Dictionary<string, string> AdditionalSettings { get; set; } = new Dictionary<string, string>();

}
