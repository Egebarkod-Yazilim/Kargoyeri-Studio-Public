// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Providers;

public sealed partial class ProviderShipmentResult
{
    public bool Success { get; set; }
    public global::Kargoyeri.Domain.Enums.ShipmentStatus Status { get; set; }
    public string TrackingNumber { get; set; }
    public string LabelUrl { get; set; }
    public string LabelContentBase64 { get; set; }
    public string Message { get; set; }
    public string RawResponse { get; set; }
    public ProviderShipmentResult() { }
    public static global::Kargoyeri.Application.Abstractions.Providers.ProviderShipmentResult Ok(global::Kargoyeri.Domain.Enums.ShipmentStatus status, string trackingNumber, string labelUrl, string labelContentBase64, string message, string rawResponse)
    {
        throw new global::System.NotImplementedException();
    }
    public static global::Kargoyeri.Application.Abstractions.Providers.ProviderShipmentResult Fail(string message, string rawResponse)
    {
        throw new global::System.NotImplementedException();
    }
}
