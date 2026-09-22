using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class YurticiProviderBlueprint : ProviderBlueprintBase
{
	public override CargoProviderType SupportedProvider => CargoProviderType.Yurtici;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "Yurtici",
			Provider = "Yurtici",
			IntegrationStyle = "Legacy SOAP createShipment envelope",
			AuthenticationStyle = "Standard and COD credential pairs",
			RecommendedIntegrationMode = "Legacy SOAP compatibility mode until official public technical contract is obtained",
			PublicDocsAvailable = false,
			LiveTransportImplemented = false,
			RequestPreviewAvailable = true,
			LegacySource = "KargoEntegre/CargoProcedures/Yurtici/YurticiKargoSiparis.cs",
			SourceNote = "Legacy mapping creates ShippingOrderVO with cargoKey and optional COD installment data.",
			SourceUrl = "https://www.yurticikargo.com/tr/online-servisler",
			OfficialDocsSummary = "Official public site exposes online services, but open technical API contract details were not publicly verifiable in the latest check.",
			LastVerifiedDate = "2026-04-09",
			SupportedOperations = new List<string> { "CreateShipment", "CancelShipment" },
			ModernizationNotes = new List<string> { "Keep current preview and configuration model flexible for future official service bindings.", "Do not assume public REST availability until a formal contract is received." },
			MetadataHints = new List<string> { "payment.type or paymentMethodSystemName", "invoice.serial and invoice.sequence" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("Username", "WS Username", required: true, secret: false, "Standard Yurtiçi username."),
				ProviderBlueprintBase.RootField("Password", "WS Password", required: true, secret: true, "Standard Yurtiçi password."),
				ProviderBlueprintBase.AdditionalField("usernameCod", "COD Username", required: false, secret: false, "Alternate username for COD calls."),
				ProviderBlueprintBase.AdditionalField("passwordCod", "COD Password", required: false, secret: true, "Alternate password for COD calls."),
				ProviderBlueprintBase.AdditionalField("installmentCount", "Installment Count", required: false, secret: false, "Required when COD card is used.", "1")
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
		bool flag = legacyPaymentKind != LegacyPaymentKind.Prepaid;
		if (flag)
		{
			if (string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetAdditionalSetting(credential, "usernameCod")))
			{
				list.Add("AdditionalSettings.usernameCod");
			}
			if (string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetAdditionalSetting(credential, "passwordCod")))
			{
				list.Add("AdditionalSettings.passwordCod");
			}
		}
		if (legacyPaymentKind == LegacyPaymentKind.CashOnDeliveryCard && string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetAdditionalSetting(credential, "installmentCount")))
		{
			list.Add("AdditionalSettings.installmentCount");
		}
		string text = LegacyProviderMappingHelpers.GenerateNumericCode(18);
		string metadataValue = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "invoice.serial", "invoiceSerial");
		string metadataValue2 = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "invoice.sequence", "invoiceSequence");
		string text2 = ((!string.IsNullOrWhiteSpace(metadataValue) && !string.IsNullOrWhiteSpace(metadataValue2)) ? (metadataValue + metadataValue2) : ("GE" + text.Substring(0, 10)));
		string wsUserName = (flag ? ProviderBlueprintBase.GetAdditionalSetting(credential, "usernameCod") : credential?.Username);
		string wsPassword = ProviderBlueprintBase.MaskSecret(flag ? ProviderBlueprintBase.GetAdditionalSetting(credential, "passwordCod") : credential?.Password);
		int packageCount = LegacyProviderMappingHelpers.GetPackageCount(shipment);
		string cargoKey = text;
		string receiverCustName = LegacyProviderMappingHelpers.ResolveRecipientName(shipment);
		string cityName = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient);
		string townName = LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient);
		string receiverPhone = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone);
		string receiverAddress = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient);
		string invoiceKey = text2;
		if (1 == 0)
		{
		}
		string ttCollectionType = legacyPaymentKind switch
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
			service = "createShipment",
			userLanguage = "tr-TR",
			wsUserName = wsUserName,
			wsPassword = wsPassword,
			ShippingOrderVO = new[]
			{
				new
				{
					cargoCount = packageCount,
					cargoKey = cargoKey,
					receiverCustName = receiverCustName,
					cityName = cityName,
					townName = townName,
					receiverPhone1 = receiverPhone,
					receiverAddress = receiverAddress,
					invoiceKey = invoiceKey,
					ttCollectionType = ttCollectionType,
					ttInvoiceAmount = (flag ? new decimal?(LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment)) : null),
					ttDocumentId = (flag ? text.Substring(0, 12) : null),
					ttDocumentSaveType = (flag ? "0" : null),
					dcCreditRule = ((legacyPaymentKind == LegacyPaymentKind.CashOnDeliveryCard) ? new int?(1) : null),
					dcSelectedCredit = ((legacyPaymentKind == LegacyPaymentKind.CashOnDeliveryCard) ? ParseLong(ProviderBlueprintBase.GetAdditionalSetting(credential, "installmentCount")) : null)
				}
			}
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, text, payload, list, null, new string[2] { "Yurtiçi cargoKey and invoice fallback are preserved from the legacy code path.", "COD card preview keeps installment fields explicit so tenant-specific card policies can be validated." });
	}

	private static long? ParseLong(string? value)
	{
		long result;
		return long.TryParse(value, out result) ? new long?(result) : null;
	}
}
