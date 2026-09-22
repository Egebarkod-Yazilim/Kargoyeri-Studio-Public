// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class UpsertCustomerRequest
{
    public string TenantKey { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public string ApiKey { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto> AllowedProviders { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Contracts.Dtos.NotificationTargetDto> NotificationTargets { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> Metadata { get; set; }
    public UpsertCustomerRequest() { }
}
