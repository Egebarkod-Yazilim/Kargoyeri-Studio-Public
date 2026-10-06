using System.Collections.Generic;
using System.Linq;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class ArasProviderBlueprint : ProviderBlueprintBase
{
	public override CargoProviderType SupportedProvider => CargoProviderType.Aras;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "Aras",
			Provider = "Aras",
			IntegrationStyle = "SOAP SetOrder / CancelDispatch",
			AuthenticationStyle = "Username/password SOAP Order nesnesi içinde",
			RecommendedIntegrationMode = "Gerçek SOAP transport: customerservicestest.araskargo.com.tr (test) / EndpointBase ile üretim URL",
			PublicDocsAvailable = true,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			LegacySource = "KargoEntegre/CargoProcedures/Aras/ArasKargoSiparis.cs",
			SourceNote = "Legacy code builds an Aras Order payload with IntegrationCode, piece details and optional COD fields.",
			SourceUrl = "https://www.araskargo.com.tr/hizmetlerimiz/kurumsal-hizmetlerimiz/entegrasyon-hizmetlerimiz",
			OfficialDocsSummary = "Aras publicly advertises integration services, but technical contract details remain onboarding-driven rather than openly documented.",
			LastVerifiedDate = "2026-04-09",
			SupportedOperations = new List<string> { "CreateShipment", "CancelShipment" },
			KnownApiProducts = new List<string> { "Integration services onboarding", "Legacy SOAP order/dispatch operations" },
			KnownEndpointHints = new List<string> { "Legacy project references test SOAP endpoints under customerservicestest.araskargo.com.tr.", "Barcode, dispatch and cancel operations exist in the old connected service contract." },
			ModernizationNotes = new List<string> { "Keep endpoint overrides configurable because Aras environments are often provisioned during onboarding.", "Transport implementation should not hardcode test URLs." },
			MetadataHints = new List<string> { "payment.type or paymentMethodSystemName", "shipping.payor", "invoice.serial and invoice.sequence" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("Username", "WS Username", required: true, secret: false, "Aras integration username."),
				ProviderBlueprintBase.RootField("Password", "WS Password", required: true, secret: true, "Aras integration password."),
				ProviderBlueprintBase.RootField("EndpointBase", "Endpoint", required: false, secret: false, "Optional service override for future transport binding.")
			}
		};
	}

	public override ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrWhiteSpace(credential?.Username))
		{
			list.Add("Root.Username");
		}
		if (string.IsNullOrWhiteSpace(credential?.Password))
		{
			list.Add("Root.Password");
		}
		LegacyPaymentKind legacyPaymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		string integrationCode = LegacyProviderMappingHelpers.GenerateNumericCode(15);
		string text = LegacyProviderMappingHelpers.ResolveInvoiceNumber(shipment, "GE", 14);
		var array = (from index in Enumerable.Range(1, LegacyProviderMappingHelpers.GetPackageCount(shipment))
			select new
			{
				BarcodeNumber = $"{integrationCode}{index}",
				VolumetricWeight = "1",
				Weight = "1"
			}).ToArray();
		var credentials = new
		{
			userName = credential?.Username,
			password = ProviderBlueprintBase.MaskSecret(credential?.Password)
		};
		string tradingWaybillNumber = text;
		string integrationCode2 = integrationCode;
		string receiverName = LegacyProviderMappingHelpers.ResolveRecipientName(shipment);
		string receiverAddress = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient);
		string receiverPhone = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone);
		string receiverCityName = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient);
		string receiverTownName = LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient);
		string pieceCount = LegacyProviderMappingHelpers.GetPackageCount(shipment).ToString();
		string payorTypeCode = (LegacyProviderMappingHelpers.RecipientPaysShipping(shipment) ? "2" : "1");
		var pieceDetails = array;
		string isCod = ((legacyPaymentKind == LegacyPaymentKind.Prepaid) ? "0" : "1");
		if (1 == 0)
		{
		}
		string codCollectionType = legacyPaymentKind switch
		{
			LegacyPaymentKind.CashOnDeliveryCash => "0", 
			LegacyPaymentKind.CashOnDeliveryCard => "1", 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		var payload = new
		{
			service = "SetOrder",
			credentials = credentials,
			order = new
			{
				TradingWaybillNumber = tradingWaybillNumber,
				IntegrationCode = integrationCode2,
				ReceiverName = receiverName,
				ReceiverAddress = receiverAddress,
				ReceiverPhone1 = receiverPhone,
				ReceiverCityName = receiverCityName,
				ReceiverTownName = receiverTownName,
				PieceCount = pieceCount,
				IsWorldWide = "0",
				PayorTypeCode = payorTypeCode,
				PieceDetails = pieceDetails,
				IsCod = isCod,
				CodCollectionType = codCollectionType,
				CodAmount = ((legacyPaymentKind == LegacyPaymentKind.Prepaid) ? null : LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment).ToString("0.##"))
			}
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, integrationCode, payload, list, null, new string[2] { "Preview preserves the legacy IntegrationCode and PieceDetails structure.", "Barcode label download from the old project is intentionally not embedded into the API layer." });
	}
}
