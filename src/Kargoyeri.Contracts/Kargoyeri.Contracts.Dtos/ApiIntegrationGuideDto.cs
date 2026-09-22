using System.Collections.Generic;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ApiIntegrationGuideDto
{
	public string ServiceName { get; set; } = string.Empty;


	public string SwaggerUrl { get; set; } = string.Empty;


	public string OpenApiUrl { get; set; } = string.Empty;


	public string HealthUrl { get; set; } = string.Empty;


	public string InfoUrl { get; set; } = string.Empty;


	public string HeaderName { get; set; } = string.Empty;


	public string AuthenticationModel { get; set; } = string.Empty;


	public string TenantResolutionModel { get; set; } = string.Empty;


	public List<string> TenantKeyRules { get; set; } = new List<string>();


	public List<string> OnboardingSteps { get; set; } = new List<string>();


	public List<string> Notes { get; set; } = new List<string>();


	public Dictionary<string, string> SampleHeaders { get; set; } = new Dictionary<string, string>();


	public Dictionary<string, string> ProviderRoutes { get; set; } = new Dictionary<string, string>();


	public CreateShipmentRequest SampleCreateShipmentRequest { get; set; } = new CreateShipmentRequest();

}
