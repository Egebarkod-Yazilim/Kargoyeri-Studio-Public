// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ProviderSettingsDto
{
    public global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto Provider { get; set; }
    public bool IsEnabled { get; set; }
    public string ClientCode { get; set; }
    public string Username { get; set; }
    public string PasswordMasked { get; set; }
    public string ApiKeyMasked { get; set; }
    public string EndpointBase { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> AdditionalSettings { get; set; }
    public global::System.DateTimeOffset UpdatedAtUtc { get; set; }
    public ProviderSettingsDto() { }
}
