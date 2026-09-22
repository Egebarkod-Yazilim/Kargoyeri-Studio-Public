using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class NotificationTargetDto
{
	public NotificationChannelDto Channel { get; set; } = NotificationChannelDto.Internal;


	public string Address { get; set; } = string.Empty;


	public bool IsEnabled { get; set; } = true;

}
