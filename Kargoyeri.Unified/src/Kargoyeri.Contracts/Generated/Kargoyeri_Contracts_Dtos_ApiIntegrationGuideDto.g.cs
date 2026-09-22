// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ApiIntegrationGuideDto
{
    public string ServiceName { get; set; }
    public string SwaggerUrl { get; set; }
    public string OpenApiUrl { get; set; }
    public string HealthUrl { get; set; }
    public string InfoUrl { get; set; }
    public string HeaderName { get; set; }
    public string AuthenticationModel { get; set; }
    public string TenantResolutionModel { get; set; }
    public global::System.Collections.Generic.List<string> TenantKeyRules { get; set; }
    public global::System.Collections.Generic.List<string> OnboardingSteps { get; set; }
    public global::System.Collections.Generic.List<string> Notes { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> SampleHeaders { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> ProviderRoutes { get; set; }
    public global::Kargoyeri.Contracts.Dtos.CreateShipmentRequest SampleCreateShipmentRequest { get; set; }
    public ApiIntegrationGuideDto() { }
}
