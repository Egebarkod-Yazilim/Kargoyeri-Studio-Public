using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class SandboxProviderBlueprint : ProviderBlueprintBase
{
	public override CargoProviderType SupportedProvider => CargoProviderType.Sandbox;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "Sandbox",
			Provider = "Sandbox",
			IntegrationStyle = "Internal simulation",
			AuthenticationStyle = "API key + tenant scoped provider settings",
			RecommendedIntegrationMode = "Built-in simulator",
			PublicDocsAvailable = false,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			SourceNote = "Sandbox provider is an internal simulator used to validate client integration without a real cargo network.",
			OfficialDocsSummary = "No external documentation. Used only for internal integration tests.",
			LastVerifiedDate = "2026-04-09",
			SupportedOperations = new List<string> { "CreateShipment", "CancelShipment", "RefreshStatus" }
		};
	}

	public override ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential)
	{
		string shipmentReference = shipment.ShipmentReference;
		int length = shipmentReference.Length;
		int num = length - 6;
		string text = "SBX-" + shipmentReference.Substring(num, length - num);
		var payload = new
		{
			operation = "create",
			shipmentReference = shipment.ShipmentReference,
			orderReference = shipment.OrderReference,
			tracking = text,
			recipient = new
			{
				shipment.Recipient.Name,
				shipment.Recipient.Phone,
				shipment.Recipient.City,
				shipment.Recipient.District
			},
			packageCount = LegacyProviderMappingHelpers.GetPackageCount(shipment)
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, text, payload, null, null, new string[1] { "Sandbox preview matches the built-in provider and can be exercised end-to-end." }, liveTransportImplemented: true);
	}
}
