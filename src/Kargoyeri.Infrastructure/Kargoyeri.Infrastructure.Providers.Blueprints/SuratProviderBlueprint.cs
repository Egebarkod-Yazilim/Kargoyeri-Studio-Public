using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class SuratProviderBlueprint : ProviderBlueprintBase
{
	public override CargoProviderType SupportedProvider => CargoProviderType.Surat;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "Surat",
			Provider = "Surat",
			IntegrationStyle = "REST JSON gonderi API (api02.suratkargo.com.tr)",
			AuthenticationStyle = "CariKodu + Sifre / panelPassword credential pairs",
			RecommendedIntegrationMode = "Gerçek REST transport: GonderiBarkodOlustur, GonderiSil, KargoTakipHareketDetayi",
			PublicDocsAvailable = true,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			LegacySource = "KargoEntegre/CargoProcedures/Surat/SuratKargoSiparis.cs",
			SourceNote = "Legacy mapping switches credentials for COD shipments and posts a Gonderi object.",
			OfficialDocsSummary = "Public technical documentation could not be verified from the official website during the latest check.",
			LastVerifiedDate = "2026-04-09",
			SupportedOperations = new List<string> { "CreateShipment", "CancelShipment" },
			ModernizationNotes = new List<string> { "Keep endpoint and credential pairs configurable because public technical guidance is limited.", "When an official contract is obtained, add a transport adapter without changing the external API." },
			MetadataHints = new List<string> { "payment.type or paymentMethodSystemName", "shipping.payor", "invoice.serial and invoice.sequence" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("Username", "WS Username", required: true, secret: false, "Standard Sürat username."),
				ProviderBlueprintBase.RootField("Password", "WS Password", required: true, secret: true, "Standard Sürat password used for delete/passive actions."),
				ProviderBlueprintBase.AdditionalField("panelPassword", "Panel Password", required: true, secret: true, "Panel password used in create call."),
				ProviderBlueprintBase.AdditionalField("packageType", "Package Type", required: true, secret: false, "Legacy package type code.", "2"),
				ProviderBlueprintBase.AdditionalField("usernameCod", "COD Username", required: false, secret: false, "Alternative username for COD shipments."),
				ProviderBlueprintBase.AdditionalField("passwordCod", "COD Password", required: false, secret: true, "Alternative password for COD cancellation."),
				ProviderBlueprintBase.AdditionalField("panelPasswordCod", "COD Panel Password", required: false, secret: true, "Alternative panel password for COD create call.")
			}
		};
	}

	public override ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential)
	{
		List<string> list = new List<string>();
		LegacyPaymentKind legacyPaymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		bool flag = legacyPaymentKind != LegacyPaymentKind.Prepaid;
		if (string.IsNullOrWhiteSpace(credential?.Username))
		{
			list.Add("Root.Username");
		}
		if (string.IsNullOrWhiteSpace(credential?.Password))
		{
			list.Add("Root.Password");
		}
		if (string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetAdditionalSetting(credential, "panelPassword")))
		{
			list.Add("AdditionalSettings.panelPassword");
		}
		if (string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetAdditionalSetting(credential, "packageType")))
		{
			list.Add("AdditionalSettings.packageType");
		}
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
			if (string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetAdditionalSetting(credential, "panelPasswordCod")))
			{
				list.Add("AdditionalSettings.panelPasswordCod");
			}
		}
		string text = LegacyProviderMappingHelpers.GenerateNumericCode(13);
		var credentials = new
		{
			userName = (flag ? ProviderBlueprintBase.GetAdditionalSetting(credential, "usernameCod") : credential?.Username),
			password = ProviderBlueprintBase.MaskSecret(flag ? ProviderBlueprintBase.GetAdditionalSetting(credential, "panelPasswordCod") : ProviderBlueprintBase.GetAdditionalSetting(credential, "panelPassword"))
		};
		int packageCount = LegacyProviderMappingHelpers.GetPackageCount(shipment);
		string aliciAdresi = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient);
		string email = shipment.Recipient.Email;
		string il = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient);
		string ilce = LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient);
		string telefonCep = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone);
		decimal birimDesi = LegacyProviderMappingHelpers.SumDesi(shipment);
		decimal birimKg = LegacyProviderMappingHelpers.SumWeight(shipment);
		string ozelKargoTakipNo = text;
		byte? kargoTuru = ParseByte(ProviderBlueprintBase.GetAdditionalSetting(credential, "packageType"));
		int odemetipi = ((!LegacyProviderMappingHelpers.RecipientPaysShipping(shipment)) ? 1 : 2);
		string kisiKurum = LegacyProviderMappingHelpers.ResolveRecipientName(shipment);
		string metadataValue = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "invoice.serial", "invoiceSerial");
		string metadataValue2 = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "invoice.sequence", "invoiceSequence");
		decimal? kapidanOdemeTutari = (flag ? new decimal?(LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment)) : null);
		if (1 == 0)
		{
		}
		int kapidanOdemeTahsilatTipi = legacyPaymentKind switch
		{
			LegacyPaymentKind.CashOnDeliveryCash => 1, 
			LegacyPaymentKind.CashOnDeliveryCard => 2, 
			_ => 0, 
		};
		if (1 == 0)
		{
		}
		var payload = new
		{
			service = "GonderiyiKargoyaGonder",
			credentials = credentials,
			gonderi = new
			{
				Adet = packageCount,
				AliciAdresi = aliciAdresi,
				Email = email,
				Il = il,
				Ilce = ilce,
				TelefonCep = telefonCep,
				BirimDesi = birimDesi,
				BirimKg = birimKg,
				OzelKargoTakipNo = ozelKargoTakipNo,
				KargoTuru = kargoTuru,
				Odemetipi = odemetipi,
				TeslimSekli = 1,
				KisiKurum = kisiKurum,
				IrsaliyeSeriNo = metadataValue,
				IrsaliyeSiraNo = metadataValue2,
				KapidanOdemeTutari = kapidanOdemeTutari,
				KapidanOdemeTahsilatTipi = kapidanOdemeTahsilatTipi
			}
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, text, payload, list, null, new string[3] { "COD shipments switch credential pairs exactly like the legacy project.", "Preview keeps invoice fields nullable because some tenants generate them upstream.", "Canli transport: POST /api/GonderiyiKargoyaGonder/GonderiBarkodOlustur" }, liveTransportImplemented: true);
	}

	private static byte? ParseByte(string? value)
	{
		byte result;
		return byte.TryParse(value, out result) ? new byte?(result) : null;
	}
}
