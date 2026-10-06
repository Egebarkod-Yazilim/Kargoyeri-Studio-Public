using System;
using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class TrendyolExpressProviderBlueprint : ProviderBlueprintBase
{
	public override CargoProviderType SupportedProvider => CargoProviderType.TrendyolExpress;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "TrendyolExpress",
			Provider = "Trendyol Express",
			IntegrationStyle = "REST Basic Auth: updateWarehouse + getShipmentPackages",
			AuthenticationStyle = "Basic Auth: Username:Password, ClientCode=SellerId",
			RecommendedIntegrationMode = "Gerçek REST transport: api.trendyol.com/sapigw/suppliers/{sellerId}/orders/",
			PublicDocsAvailable = true,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			SourceNote = "Public Trendyol docs expose shipment package stream endpoints and warehouse update flow specifically for sellers using Trendyol Express.",
			SourceUrl = "https://developers.trendyol.com/v2.0/docs/update-warehouse-information",
			OfficialDocsSummary = "Official Trendyol developer docs document order package stream retrieval and warehouse update flow for sellers using Trendyol Express.",
			LastVerifiedDate = "2026-04-10",
			SupportedOperations = new List<string> { "RefreshStatus", "OrderPackageSync", "WarehouseUpdate" },
			KnownApiProducts = new List<string> { "getShipmentPackagesStream", "updateWarehouse" },
			KnownEndpointHints = new List<string> { "Shipment package stream uses cursor based pagination and lastModified filters.", "Warehouse update endpoint is available only for sellers using Trendyol Express." },
			ModernizationNotes = new List<string> { "This provider is source-system oriented; it should focus on package sync and status orchestration rather than classical carrier label creation.", "Preview and live implementation should map marketplace package lifecycle into the shared shipment status model." },
			MetadataHints = new List<string> { "trendyol.packageId", "trendyol.sellerId", "trendyol.warehouseId", "trendyol.packageStatus" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("Username", "API Username", required: true, secret: false, "Trendyol API username / basic auth user."),
				ProviderBlueprintBase.RootField("Password", "API Password", required: true, secret: true, "Trendyol API password / basic auth secret."),
				ProviderBlueprintBase.RootField("ClientCode", "Seller Id", required: true, secret: false, "Seller id used in shipment package endpoints."),
				ProviderBlueprintBase.RootField("EndpointBase", "Endpoint Base", required: false, secret: false, "Optional gateway override for stage or production."),
				ProviderBlueprintBase.AdditionalField("storeFrontId", "Storefront Id", required: false, secret: false, "Optional storefront identifier used by the seller."),
				ProviderBlueprintBase.AdditionalField("integrationMode", "Integration Mode", required: false, secret: false, "Suggested values: ShipmentPackageStream, Simulation", "ShipmentPackageStream"),
				ProviderBlueprintBase.AdditionalField("warehouseId", "Warehouse Id", required: false, secret: false, "Warehouse id used when Trendyol Express warehouse update is needed.")
			}
		};
	}

	public override ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential)
	{
		List<string> list = new List<string>();
		RequireRoot(credential, "Username", list);
		RequireRoot(credential, "Password", list);
		RequireRoot(credential, "ClientCode", list);
		string sellerId = credential?.ClientCode;
		string text = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "trendyol.packageId", "packageId") ?? shipment.OrderReference;
		string warehouseId = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "trendyol.warehouseId", "warehouseId") ?? ProviderBlueprintBase.GetAdditionalSetting(credential, "warehouseId");
		var payload = new
		{
			sync = new
			{
				method = "getShipmentPackagesStream",
				sellerId = sellerId,
				lastModifiedStartDate = DateTimeOffset.UtcNow.AddDays(-7.0).ToUnixTimeMilliseconds(),
				packageItemStatuses = "Created,Picking,Invoiced,Shipped,Delivered"
			},
			warehouseUpdate = new
			{
				method = "updateWarehouse",
				sellerId = sellerId,
				packageId = text,
				warehouseId = warehouseId
			},
			auth = new
			{
				basicUser = credential?.Username,
				basicPassword = ProviderBlueprintBase.MaskSecret(credential?.Password)
			}
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, text, payload, list, null, new string[2] { "Trendyol Express is modeled as a shipment package orchestration provider rather than a classic direct-label carrier.", "Warehouse update is only relevant for sellers using Trendyol Express." });
	}

	private static void RequireRoot(ProviderCredential? credential, string key, List<string> missing)
	{
		if (string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetRootSetting(credential, key)))
		{
			missing.Add("Root." + key);
		}
	}
}
