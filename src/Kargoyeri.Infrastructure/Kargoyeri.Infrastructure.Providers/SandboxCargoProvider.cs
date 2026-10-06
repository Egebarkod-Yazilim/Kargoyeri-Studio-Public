using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class SandboxCargoProvider : ICargoProvider
{
	public CargoProviderType SupportedProvider => CargoProviderType.Sandbox;

	public Task<ProviderShipmentResult> CreateShipmentAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(5, 2);
		defaultInterpolatedStringHandler.AppendLiteral("SBX-");
		defaultInterpolatedStringHandler.AppendFormatted(DateTimeOffset.UtcNow, "yyyyMMddHHmmss");
		defaultInterpolatedStringHandler.AppendLiteral("-");
		string shipmentReference = shipment.ShipmentReference;
		int length = shipmentReference.Length;
		int num = length - 6;
		defaultInterpolatedStringHandler.AppendFormatted(shipmentReference.Substring(num, length - num));
		string text = defaultInterpolatedStringHandler.ToStringAndClear();
		string labelUrl = "/sandbox/labels/" + shipment.ShipmentReference + ".txt";
		string s = $"Kargoyeri Sandbox Label{Environment.NewLine}Shipment: {shipment.ShipmentReference}{Environment.NewLine}Tracking: {text}";
		string labelContentBase = Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
		return Task.FromResult(ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text, labelUrl, labelContentBase, "Sandbox provider accepted the shipment.", "{\"provider\":\"sandbox\",\"phase\":\"create\"}"));
	}

	public Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (shipment.Status == ShipmentStatus.Delivered)
		{
			return Task.FromResult(ProviderShipmentResult.Fail("Delivered shipment can not be cancelled.", "{\"provider\":\"sandbox\",\"phase\":\"cancel\",\"result\":\"denied\"}"));
		}
		return Task.FromResult(ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, shipment.LabelUrl, shipment.LabelContentBase64, string.IsNullOrWhiteSpace(reason) ? "Shipment cancelled." : ("Shipment cancelled: " + reason), "{\"provider\":\"sandbox\",\"phase\":\"cancel\",\"result\":\"ok\"}"));
	}

	public Task<ProviderShipmentResult> RefreshStatusAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		ShipmentStatus status = shipment.Status;
		if (1 == 0)
		{
		}
		ShipmentStatus shipmentStatus;
		switch (status)
		{
		case ShipmentStatus.Pending:
			shipmentStatus = ShipmentStatus.ProviderAccepted;
			break;
		case ShipmentStatus.ProviderAccepted:
			shipmentStatus = ShipmentStatus.InTransit;
			break;
		case ShipmentStatus.InTransit:
			if (shipment.RetryCount < 2)
			{
				goto default;
			}
			shipmentStatus = ShipmentStatus.Delivered;
			break;
		default:
			shipmentStatus = shipment.Status;
			break;
		}
		if (1 == 0)
		{
		}
		ShipmentStatus shipmentStatus2 = shipmentStatus;
		return Task.FromResult(ProviderShipmentResult.Ok(shipmentStatus2, shipment.TrackingNumber, shipment.LabelUrl, shipment.LabelContentBase64, $"Sandbox status refreshed to {shipmentStatus2}.", "{\"provider\":\"sandbox\",\"phase\":\"refresh\",\"status\":\"{{status}}\"}"));
	}
}
