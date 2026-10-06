// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Domain.Entities;

public sealed partial class ProviderCredential
{
    public string TenantKey { get; set; }
    public global::Kargoyeri.Domain.Enums.CargoProviderType Provider { get; set; }
    public bool IsEnabled { get; set; }
    public string ClientCode { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string ApiKey { get; set; }
    public string EndpointBase { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> AdditionalSettings { get; set; }
    public global::System.DateTimeOffset UpdatedAtUtc { get; set; }
    public ProviderCredential() { }
}
