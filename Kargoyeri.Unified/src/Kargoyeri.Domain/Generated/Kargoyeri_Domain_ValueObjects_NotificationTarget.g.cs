// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Domain.ValueObjects;

public sealed partial class NotificationTarget
{
    public global::Kargoyeri.Domain.Enums.NotificationChannel Channel { get; set; }
    public string Address { get; set; }
    public bool IsEnabled { get; set; }
    public NotificationTarget() { }
}
