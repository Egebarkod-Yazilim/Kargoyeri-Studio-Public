namespace Kargoyeri.Contracts.Enums;

public enum ShipmentStatusDto
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
