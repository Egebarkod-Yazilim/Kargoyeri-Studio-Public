using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Infrastructure.Providers;

internal abstract class PreviewBackedCargoProviderBase : ICargoProvider
{
	private readonly IProviderBlueprint _blueprint;

	private readonly ILogger _logger;

	public CargoProviderType SupportedProvider { get; }

	protected PreviewBackedCargoProviderBase(CargoProviderType supportedProvider, IEnumerable<IProviderBlueprint> blueprints, ILogger logger)
	{
		SupportedProvider = supportedProvider;
		_blueprint = blueprints.Single((IProviderBlueprint x) => x.SupportedProvider == supportedProvider);
		_logger = logger;
	}

	public Task<ProviderShipmentResult> CreateShipmentAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		if (!IsSimulationEnabled(credential))
		{
			_logger.LogWarning("Customer {CustomerCode} attempted live create for provider {Provider} but transport is not wired.", shipment.TenantKey, SupportedProvider);
			return Task.FromResult(ProviderShipmentResult.Fail($"{SupportedProvider} için canlı transport henüz bağlı değil. Ayarlarda simulationMode=true vererek entegrasyon testi yapabilirsin.", $"{{\"provider\":\"{SupportedProvider}\",\"mode\":\"preview-only\",\"operation\":\"create\"}}"));
		}
		ProviderRequestPreviewResponse providerRequestPreviewResponse = _blueprint.PreviewCreateShipment(shipment, credential);
		string text = providerRequestPreviewResponse.TrackingNumberCandidate;
		if (text == null)
		{
			string shipmentReference = shipment.ShipmentReference;
			int length = shipmentReference.Length;
			int num = length - 8;
			text = "SIM-" + shipmentReference.Substring(num, length - num);
		}
		string text2 = text;
		_logger.LogInformation("Customer {CustomerCode} created simulated shipment for provider {Provider} with tracking {TrackingNumber}", shipment.TenantKey, SupportedProvider, text2);
		return Task.FromResult(ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text2, null, null, $"{SupportedProvider} simulation mode shipment created.", providerRequestPreviewResponse.PayloadPreview));
	}

	public Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (!IsSimulationEnabled(credential))
		{
			return Task.FromResult(ProviderShipmentResult.Fail($"{SupportedProvider} için canlı iptal transportu henüz bağlı değil.", $"{{\"provider\":\"{SupportedProvider}\",\"mode\":\"preview-only\",\"operation\":\"cancel\"}}"));
		}
		_logger.LogInformation("Customer {CustomerCode} cancelled simulated shipment for provider {Provider}. Reason: {Reason}", shipment.TenantKey, SupportedProvider, reason ?? "not-provided");
		return Task.FromResult(ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, shipment.LabelUrl, shipment.LabelContentBase64, string.IsNullOrWhiteSpace(reason) ? $"{SupportedProvider} simulation mode shipment cancelled." : $"{SupportedProvider} simulation mode shipment cancelled: {reason}", $"{{\"provider\":\"{SupportedProvider}\",\"mode\":\"simulation\",\"operation\":\"cancel\"}}"));
	}

	public Task<ProviderShipmentResult> RefreshStatusAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		if (!IsSimulationEnabled(credential))
		{
			return Task.FromResult(ProviderShipmentResult.Fail($"{SupportedProvider} için canlı durum yenileme transportu henüz bağlı değil.", $"{{\"provider\":\"{SupportedProvider}\",\"mode\":\"preview-only\",\"operation\":\"refresh\"}}"));
		}
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
		_logger.LogInformation("Customer {CustomerCode} refreshed simulated shipment for provider {Provider} to {Status}", shipment.TenantKey, SupportedProvider, shipmentStatus2);
		return Task.FromResult(ProviderShipmentResult.Ok(shipmentStatus2, shipment.TrackingNumber, shipment.LabelUrl, shipment.LabelContentBase64, $"{SupportedProvider} simulation mode status refreshed to {shipmentStatus2}.", $"{{\"provider\":\"{SupportedProvider}\",\"mode\":\"simulation\",\"operation\":\"refresh\",\"status\":\"{shipmentStatus2}\"}}"));
	}

	private static bool IsSimulationEnabled(ProviderCredential? credential)
	{
		if (credential?.AdditionalSettings == null)
		{
			return false;
		}
		foreach (KeyValuePair<string, string> additionalSetting in credential.AdditionalSettings)
		{
			if (!string.Equals(additionalSetting.Key, "simulationMode", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			return additionalSetting.Value != null && (additionalSetting.Value.Equals("true", StringComparison.OrdinalIgnoreCase) || additionalSetting.Value.Equals("1", StringComparison.OrdinalIgnoreCase) || additionalSetting.Value.Equals("yes", StringComparison.OrdinalIgnoreCase));
		}
		return false;
	}
}
