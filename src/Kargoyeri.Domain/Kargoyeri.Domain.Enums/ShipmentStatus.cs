namespace Kargoyeri.Domain.Enums;

public enum ShipmentStatus
{
	Pending,
	Queued,
	ProviderAccepted,
	LabelReady,
	InTransit,
	Delivered,
	Cancelled,
	Failed
}
