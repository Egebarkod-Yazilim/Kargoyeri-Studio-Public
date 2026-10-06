// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ProviderProfileDto
{
    public string ProviderCode { get; set; }
    public string Provider { get; set; }
    public string IntegrationStyle { get; set; }
    public string AuthenticationStyle { get; set; }
    public string RecommendedIntegrationMode { get; set; }
    public bool PublicDocsAvailable { get; set; }
    public bool LiveTransportImplemented { get; set; }
    public bool RequestPreviewAvailable { get; set; }
    public string LegacySource { get; set; }
    public string SourceNote { get; set; }
    public string SourceUrl { get; set; }
    public string OfficialDocsSummary { get; set; }
    public string LastVerifiedDate { get; set; }
    public global::System.Collections.Generic.List<string> SupportedOperations { get; set; }
    public global::System.Collections.Generic.List<string> KnownApiProducts { get; set; }
    public global::System.Collections.Generic.List<string> KnownEndpointHints { get; set; }
    public global::System.Collections.Generic.List<string> ModernizationNotes { get; set; }
    public global::System.Collections.Generic.List<string> MetadataHints { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Contracts.Dtos.ProviderSettingFieldDto> SettingsSchema { get; set; }
    public ProviderProfileDto() { }
}
