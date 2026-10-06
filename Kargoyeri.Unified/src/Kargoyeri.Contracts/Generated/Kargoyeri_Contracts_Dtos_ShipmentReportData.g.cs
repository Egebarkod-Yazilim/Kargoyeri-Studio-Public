// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ShipmentReportData
{
    public global::System.DateTime FromUtc { get; set; }
    public global::System.DateTime ToUtc { get; set; }
    public int TotalCount { get; set; }
    public global::System.Collections.Generic.Dictionary<string, int> ByStatus { get; set; }
    public global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Contracts.Dtos.ProviderReportRow> ByProvider { get; set; }
    public ShipmentReportData() { }
}
