// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Domain.Entities;

public sealed partial class CustomerTenant
{
    public string TenantKey { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public string ApiKeyHash { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Domain.Enums.CargoProviderType> AllowedProviders { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Domain.ValueObjects.NotificationTarget> NotificationTargets { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> Metadata { get; set; }
    public global::System.DateTimeOffset CreatedAtUtc { get; set; }
    public global::System.DateTimeOffset UpdatedAtUtc { get; set; }
    public CustomerTenant() { }
    public bool CanUseProvider(global::Kargoyeri.Domain.Enums.CargoProviderType provider)
    {
        throw new global::System.NotImplementedException();
    }
}
