// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Domain.Entities;

public sealed partial class ShipmentOperationLog
{
    public global::System.Guid Id { get; set; }
    public string TenantKey { get; set; }
    public string ShipmentReference { get; set; }
    public global::Kargoyeri.Domain.Enums.CargoOperationType Operation { get; set; }
    public global::Kargoyeri.Domain.Enums.LogSeverity Severity { get; set; }
    public string Message { get; set; }
    public string ProviderPayload { get; set; }
    public global::System.DateTimeOffset OccurredAtUtc { get; set; }
    public ShipmentOperationLog() { }
}
