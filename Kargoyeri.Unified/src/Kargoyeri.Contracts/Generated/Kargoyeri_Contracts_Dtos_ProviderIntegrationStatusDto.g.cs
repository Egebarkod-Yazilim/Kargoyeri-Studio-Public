// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ProviderIntegrationStatusDto
{
    public string CustomerCode { get; set; }
    public global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto Provider { get; set; }
    public bool Configured { get; set; }
    public bool LiveTransportImplemented { get; set; }
    public bool SimulationEnabled { get; set; }
    public bool CanCreateShipment { get; set; }
    public string IntegrationMode { get; set; }
    public string RecommendedIntegrationMode { get; set; }
    public string SourceUrl { get; set; }
    public string OfficialDocsSummary { get; set; }
    public string RecommendedAction { get; set; }
    public global::System.Collections.Generic.List<string> MissingSettings { get; set; }
    public global::System.Collections.Generic.List<string> KnownApiProducts { get; set; }
    public global::System.Collections.Generic.List<string> KnownEndpointHints { get; set; }
    public global::System.Collections.Generic.List<string> ModernizationNotes { get; set; }
    public global::System.DateTimeOffset CheckedAtUtc { get; set; }
    public ProviderIntegrationStatusDto() { }
}
