// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ProviderRequestPreviewResponse
{
    public global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto Provider { get; set; }
    public string Operation { get; set; }
    public bool RequestMapped { get; set; }
    public bool LiveTransportImplemented { get; set; }
    public string ShipmentReference { get; set; }
    public string TrackingNumberCandidate { get; set; }
    public string PayloadFormat { get; set; }
    public string PayloadPreview { get; set; }
    public global::System.Collections.Generic.List<string> MissingConfiguration { get; set; }
    public global::System.Collections.Generic.List<string> MissingMetadata { get; set; }
    public global::System.Collections.Generic.List<string> Notes { get; set; }
    public ProviderRequestPreviewResponse() { }
}
