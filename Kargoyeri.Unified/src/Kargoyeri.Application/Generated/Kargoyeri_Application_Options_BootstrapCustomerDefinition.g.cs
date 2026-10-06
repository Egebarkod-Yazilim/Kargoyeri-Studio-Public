// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Options;

public sealed partial class BootstrapCustomerDefinition
{
    public string TenantKey { get; set; }
    public string Name { get; set; }
    public string ApiKey { get; set; }
    public bool IsActive { get; set; }
    public global::System.Collections.Generic.List<string> AllowedProviders { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Contracts.Dtos.NotificationTargetDto> NotificationTargets { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> Metadata { get; set; }
    public BootstrapCustomerDefinition() { }
}
