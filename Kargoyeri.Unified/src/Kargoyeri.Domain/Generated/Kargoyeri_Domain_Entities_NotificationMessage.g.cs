// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Domain.Entities;

public sealed partial class NotificationMessage
{
    public global::System.Guid Id { get; set; }
    public string TenantKey { get; set; }
    public string ShipmentReference { get; set; }
    public global::Kargoyeri.Domain.Enums.NotificationEventType EventType { get; set; }
    public global::Kargoyeri.Domain.Enums.NotificationChannel Channel { get; set; }
    public global::Kargoyeri.Domain.Enums.NotificationDeliveryStatus Status { get; set; }
    public string Address { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public string ErrorMessage { get; set; }
    public global::System.DateTimeOffset CreatedAtUtc { get; set; }
    public global::System.DateTimeOffset? DeliveredAtUtc { get; set; }
    public NotificationMessage() { }
    public void MarkDelivered()
    {
        throw new global::System.NotImplementedException();
    }
    public void MarkFailed(string error)
    {
        throw new global::System.NotImplementedException();
    }
    public void MarkIgnored(string message)
    {
        throw new global::System.NotImplementedException();
    }
}
