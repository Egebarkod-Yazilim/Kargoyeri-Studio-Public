using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;

namespace Kargoyeri.Application.Options;

public sealed class BootstrapCustomerDefinition
{
	public string TenantKey { get; set; } = string.Empty;


	public string Name { get; set; } = string.Empty;


	public string ApiKey { get; set; } = string.Empty;


	public bool IsActive { get; set; } = true;


	public List<string> AllowedProviders { get; set; } = new List<string>();


	public List<NotificationTargetDto> NotificationTargets { get; set; } = new List<NotificationTargetDto>();


	public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

}
