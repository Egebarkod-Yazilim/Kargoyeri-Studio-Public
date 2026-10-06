using System.Collections.Generic;

namespace Kargoyeri.Contracts.Dtos;

public sealed class TenantOnboardingResponse
{
	public string TenantKey { get; set; } = string.Empty;


	public string ApiKey { get; set; } = string.Empty;


	public CustomerProfileDto Customer { get; set; } = new CustomerProfileDto();


	public Dictionary<string, string> UsefulLinks { get; set; } = new Dictionary<string, string>();

}
