// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ProviderReportRow
{
    public string Provider { get; set; }
    public int Total { get; set; }
    public int Delivered { get; set; }
    public int Failed { get; set; }
    public int InTransit { get; set; }
    public int Pending { get; set; }
    public double SuccessRate { get; }
    public ProviderReportRow() { }
}
