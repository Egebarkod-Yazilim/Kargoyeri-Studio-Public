using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class MngProviderBlueprint : ProviderBlueprintBase
{
	public override CargoProviderType SupportedProvider => CargoProviderType.Mng;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "Mng",
			Provider = "MNG",
			IntegrationStyle = "SOAP SiparisGirisiDetayliV2 / MusteriSiparisIptal",
			AuthenticationStyle = "Username/password SOAP parametreleri",
			RecommendedIntegrationMode = "Gerçek SOAP transport: service.mngkargo.com.tr (EndpointBase ile ayarlanır)",
			PublicDocsAvailable = true,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			LegacySource = "KargoEntegre/CargoProcedures/MNG/MNGKargoSiparis.cs",
			SourceNote = "The legacy project builds a SiparisGirisiDetayliV2 request with numeric barkod and package content string.",
			SourceUrl = "https://sandbox.mngkargo.com.tr/index.php/zh-hans/product/5906",
			OfficialDocsSummary = "Official MNG sandbox portal publicly lists REST products such as Identity, Standard Query, Bulk Query and Plus Command with plan/rate details.",
			LastVerifiedDate = "2026-04-09",
			SupportedOperations = new List<string> { "CreateShipment", "CancelShipment", "RefreshStatus" },
			KnownApiProducts = new List<string> { "Identity API", "Standard Query API", "Bulk Query API", "Plus Command API" },
			KnownEndpointHints = new List<string> { "REST token/identity flow is publicly documented in the sandbox portal.", "Bulk and standard query products are better suited for worker-based status refresh than one-by-one legacy polling." },
			ModernizationNotes = new List<string> { "New transport implementation should prefer portal REST products over legacy SOAP clients.", "Provider settings should be able to hold subscription/token style values in addition to username/password." },
			MetadataHints = new List<string> { "payment.type or paymentMethodSystemName", "shipping.payor", "invoice.serial and invoice.sequence" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("Username", "WS Username", required: true, secret: false, "Legacy MNG web service username."),
				ProviderBlueprintBase.RootField("Password", "WS Password", required: true, secret: true, "Legacy MNG web service password."),
				ProviderBlueprintBase.RootField("ApiKey", "REST API Key", required: false, secret: true, "REST portal subscription/api key if modern MNG products are used."),
				ProviderBlueprintBase.RootField("EndpointBase", "Endpoint", required: false, secret: false, "Optional endpoint override for live transport."),
				ProviderBlueprintBase.AdditionalField("integrationMode", "Integration Mode", required: false, secret: false, "Suggested values: LegacySoap, RestStandard, RestPlus", "RestStandard"),
				ProviderBlueprintBase.AdditionalField("identityUrl", "Identity URL", required: false, secret: false, "REST identity/token endpoint if modern MNG products are used."),
				ProviderBlueprintBase.AdditionalField("queryBaseUrl", "Query Base URL", required: false, secret: false, "REST query base URL used by worker refresh."),
				ProviderBlueprintBase.AdditionalField("commandBaseUrl", "Command Base URL", required: false, secret: false, "REST command base URL used for create/cancel operations.")
			}
		};
	}

	public override ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential)
	{
		string text = LegacyProviderMappingHelpers.GenerateNumericCode(10);
		string pChOdemeTipi = (LegacyProviderMappingHelpers.RecipientPaysShipping(shipment) ? "U" : "P");
		LegacyPaymentKind legacyPaymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		string pChIrsaliyeNo = LegacyProviderMappingHelpers.ResolveInvoiceNumber(shipment, "GE", 8);
		List<string> list = new List<string>();
		if (string.IsNullOrWhiteSpace(credential?.Username))
		{
			list.Add("Root.Username");
		}
		if (string.IsNullOrWhiteSpace(credential?.Password))
		{
			list.Add("Root.Password");
		}
		var payload = new
		{
			service = "SiparisGirisiDetayliV2",
			credentials = new
			{
				wsUsername = credential?.Username,
				wsPassword = ProviderBlueprintBase.MaskSecret(credential?.Password)
			},
			request = new
			{
				pChIrsaliyeNo = pChIrsaliyeNo,
				pChSiparisTutar = LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment).ToString("0.##"),
				pChBarkod = text,
				pChIcerik = "Urun",
				pNParcaSayisi = LegacyProviderMappingHelpers.GetPackageCount(shipment),
				pChPaketIcerigi = LegacyProviderMappingHelpers.BuildPackageContent(shipment),
				pChAliciAdi = LegacyProviderMappingHelpers.ResolveRecipientName(shipment),
				pChSiparisNo = text,
				pChOdemeTipi = pChOdemeTipi,
				pChSehir = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient),
				pChIlce = LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient),
				pChAdres = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient),
				pChTelefon = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone),
				pChEmail = shipment.Recipient.Email,
				pNKapidaOdeme = ((legacyPaymentKind != 0) ? 1 : 0)
			}
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, text, payload, list, null, new string[2] { "This preview mirrors the parameter order of the legacy MNG SOAP call.", "Live transport is not wired yet; preview is meant to validate mapping and tenant configuration." });
	}
}
