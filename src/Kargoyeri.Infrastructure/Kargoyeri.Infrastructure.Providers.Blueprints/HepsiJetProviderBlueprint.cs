using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class HepsiJetProviderBlueprint : ProviderBlueprintBase
{
	public override CargoProviderType SupportedProvider => CargoProviderType.HepsiJet;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "HepsiJet",
			Provider = "HepsiJET",
			IntegrationStyle = "Official REST delivery API (sendDeliveryOrderEnhanced)",
			AuthenticationStyle = "Token tabanlı: GET /auth/getToken → X-Auth-Token header",
			RecommendedIntegrationMode = "Gerçek HepsiJET delivery API: token al, gonderi oluştur, takip et",
			PublicDocsAvailable = true,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			SourceNote = "HepsiJET public docs expose delivery endpoints such as sendDeliveryOrderEnhanced and barcode generation under integration-apitest.hepsijet.com.",
			SourceUrl = "https://developers.hepsiburada.com/hepsiburada/reference/get_delivery-generatezplbarcode-fpr2000000000-1",
			OfficialDocsSummary = "Official Hepsiburada developer docs publicly show HepsiJET delivery endpoints and note that live URLs are formed by removing '-sit' from test endpoints.",
			LastVerifiedDate = "2026-04-10",
			SupportedOperations = new List<string> { "CreateShipment", "CancelShipment", "RefreshStatus", "Label" },
			KnownApiProducts = new List<string> { "sendDeliveryOrderEnhanced", "generateZplBarcode", "findAvailableDeliveryDatesV2" },
			KnownEndpointHints = new List<string> { "Public docs state that sample endpoints are SIT and live URLs are derived by removing '-sit' or apitest markers.", "Returned shipments use HepsiJET as fixed company name in sample payloads." },
			ModernizationNotes = new List<string> { "Transport should support enhanced delivery payloads before direct live binding.", "Barcode label response should be abstracted into the shared label contract instead of raw file writes." },
			MetadataHints = new List<string> { "hepsijet.deliveryType", "hepsijet.parcelCount", "payment.type" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("Username", "API Username", required: true, secret: false, "HepsiJET integration username."),
				ProviderBlueprintBase.RootField("Password", "API Password", required: true, secret: true, "HepsiJET integration password."),
				ProviderBlueprintBase.RootField("ClientCode", "Client Code", required: true, secret: false, "Merchant or company client code."),
				ProviderBlueprintBase.RootField("EndpointBase", "Endpoint Base", required: false, secret: false, "Optional endpoint override for SIT or live URL."),
				ProviderBlueprintBase.AdditionalField("companyName", "Company Name", required: true, secret: false, "Sender company name.", "HepsiJET"),
				ProviderBlueprintBase.AdditionalField("taxNumber", "Tax Number", required: false, secret: false, "Merchant tax number."),
				ProviderBlueprintBase.AdditionalField("integrationMode", "Integration Mode", required: false, secret: false, "Suggested values: DeliveryApi, Simulation", "DeliveryApi"),
				ProviderBlueprintBase.AdditionalField("barcodeMode", "Barcode Mode", required: false, secret: false, "Suggested values: Zpl, Pdf", "Zpl")
			}
		};
	}

	public override ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential)
	{
		List<string> list = new List<string>();
		RequireRoot(credential, "Username", list);
		RequireRoot(credential, "Password", list);
		RequireRoot(credential, "ClientCode", list);
		RequireAdditional(credential, "companyName", list);
		string deliveryType = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "hepsijet.deliveryType", "deliveryType") ?? "STANDARD";
		string totalParcel = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "hepsijet.parcelCount", "parcelCount") ?? LegacyProviderMappingHelpers.GetPackageCount(shipment).ToString();
		string text = "HJ-" + LegacyProviderMappingHelpers.GenerateNumericCode(10);
		var payload = new
		{
			service = "sendDeliveryOrderEnhanced",
			credentials = new
			{
				userName = credential?.Username,
				password = ProviderBlueprintBase.MaskSecret(credential?.Password),
				clientCode = credential?.ClientCode
			},
			order = new
			{
				company = new
				{
					name = (ProviderBlueprintBase.GetAdditionalSetting(credential, "companyName") ?? "HepsiJET")
				},
				barcode = text,
				totalParcel = totalParcel,
				deliveryType = deliveryType,
				customer = new
				{
					name = LegacyProviderMappingHelpers.ResolveRecipientName(shipment),
					phone = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone),
					email = shipment.Recipient.Email
				},
				address = new
				{
					city = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient),
					town = LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient),
					addressLine = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient)
				},
				codAmount = LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment),
				paymentType = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment).ToString()
			}
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, text, payload, list, null, new string[3] { "HepsiJET preview models the enhanced delivery order shape from public docs.", "Canli transport: GET /auth/getToken → POST /sendDeliveryOrderEnhanced", "Test URL: https://integration-apitest.hepsijet.com — Canli URL: https://integration-api.hepsijet.com" }, liveTransportImplemented: true);
	}

	private static void RequireRoot(ProviderCredential? credential, string key, List<string> missing)
	{
		if (string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetRootSetting(credential, key)))
		{
			missing.Add("Root." + key);
		}
	}

	private static void RequireAdditional(ProviderCredential? credential, string key, List<string> missing)
	{
		if (string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetAdditionalSetting(credential, key)))
		{
			missing.Add("AdditionalSettings." + key);
		}
	}
}
