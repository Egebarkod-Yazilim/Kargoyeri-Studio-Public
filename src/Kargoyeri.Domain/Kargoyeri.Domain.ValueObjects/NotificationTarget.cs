using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Domain.ValueObjects;

public sealed class NotificationTarget
{
	public NotificationChannel Channel { get; set; } = NotificationChannel.Internal;


	public string Address { get; set; } = string.Empty;


	public bool IsEnabled { get; set; } = true;

}
