// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class NotificationMessageDto
{
    public global::System.Guid Id { get; set; }
    public string CustomerCode { get; set; }
    public string ShipmentReference { get; set; }
    public global::Kargoyeri.Contracts.Enums.NotificationEventTypeDto EventType { get; set; }
    public global::Kargoyeri.Contracts.Enums.NotificationChannelDto Channel { get; set; }
    public global::Kargoyeri.Contracts.Enums.NotificationDeliveryStatusDto Status { get; set; }
    public string Address { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public string ErrorMessage { get; set; }
    public global::System.DateTimeOffset CreatedAtUtc { get; set; }
    public global::System.DateTimeOffset? DeliveredAtUtc { get; set; }
    public NotificationMessageDto() { }
}
