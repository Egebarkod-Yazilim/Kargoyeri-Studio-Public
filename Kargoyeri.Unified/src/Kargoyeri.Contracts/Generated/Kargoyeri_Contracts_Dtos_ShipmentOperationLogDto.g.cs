// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class ShipmentOperationLogDto
{
    public string ShipmentReference { get; set; }
    public string Operation { get; set; }
    public string Severity { get; set; }
    public string Message { get; set; }
    public string ProviderPayload { get; set; }
    public global::System.DateTimeOffset OccurredAtUtc { get; set; }
    public ShipmentOperationLogDto() { }
}
