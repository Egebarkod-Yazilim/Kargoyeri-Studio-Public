// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class TenantOnboardingResponse
{
    public string TenantKey { get; set; }
    public string ApiKey { get; set; }
    public global::Kargoyeri.Contracts.Dtos.CustomerProfileDto Customer { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> UsefulLinks { get; set; }
    public TenantOnboardingResponse() { }
}
