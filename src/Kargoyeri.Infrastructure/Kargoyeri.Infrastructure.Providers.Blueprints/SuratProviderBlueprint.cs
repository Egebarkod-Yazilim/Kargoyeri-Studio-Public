using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Infrastructure.Providers;

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
			IntegrationStyle = "REST (api01/api02) + SOAP GonderiyiKargoyaGonderYeni",
			AuthenticationStyle = "CariKodu + Sifre (ClientCode + Password)",
			RecommendedIntegrationMode = "Test: api02 REST KargoTakipHareketDetayi; canlı: api01. SOAP create: webservices/prova services.asmx",
			PublicDocsAvailable = true,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			LegacySource = "KargoEntegre/CargoProcedures/Surat/SuratKargoSiparis.cs",
			SourceNote = "2024 SOAP GonderiyiKargoyaGonderYeni + REST KargoTakipHareketDetayi v2.",
			OfficialDocsSummary = "Gönderi: SOAP GonderiyiKargoyaGonderYeni. Takip: POST /api/KargoTakipHareketDetayi (CariKodu, Sifre, WebSiparisKodu). Durum kodları 1-16.",
			LastVerifiedDate = "2026-10-05",
			SupportedOperations = new List<string> { "CreateShipment", "CancelShipment", "TrackShipment" },
			ModernizationNotes = new List<string>
			{
				"CariKodu Studio ClientCode alanından, Sifre Password alanından okunur.",
				"transport=soap veya EndpointBase services.asmx ise SOAP create/track kullanılır.",
				"Kapıdan ödemede IrsaliyeSeriNo (max 5) ve IrsaliyeSiraNo (max 10) zorunludur."
			},
			MetadataHints = new List<string> { "payment.type or paymentMethodSystemName", "shipping.payor", "invoice.serial and invoice.sequence", "surat.teslimSekli", "surat.entegrasyonFirmasi" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("ClientCode", "Cari Kodu", required: true, secret: false, "Sürat tarafından verilen kullanıcı adı (CariKodu)."),
				ProviderBlueprintBase.RootField("Password", "Şifre", required: true, secret: true, "Sürat kullanıcı şifresi (Sifre)."),
				ProviderBlueprintBase.RootField("EndpointBase", "API Adresi", required: false, secret: false, "REST test: https://api02.suratkargo.com.tr | REST canlı: https://api01.suratkargo.com.tr | SOAP test: https://prova.suratkargo.com.tr/services.asmx"),
				ProviderBlueprintBase.AdditionalField("packageType", "Kargo Türü", required: false, secret: false, "1 Dosya, 2 Mi, 3 Koli.", "2"),
				ProviderBlueprintBase.AdditionalField("transport", "Transport", required: false, secret: false, "rest veya soap. Boş bırakılırsa endpoint adresinden anlaşılır."),
				ProviderBlueprintBase.AdditionalField("usernameCod", "COD Cari Kodu", required: false, secret: false, "Kapıdan ödeme gönderileri için ayrı cari kodu."),
				ProviderBlueprintBase.AdditionalField("panelPasswordCod", "COD Şifre", required: false, secret: true, "Kapıdan ödeme gönderileri için ayrı şifre."),
				ProviderBlueprintBase.AdditionalField("ekHizmetler", "Ek Hizmetler", required: false, secret: false, "Virgülle: GondericiyeSms, TelefonIhbar, AliciyaSms, AdrestenAlim")
			}
		};
	}

	public override ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential)
	{
		List<string> missing = new();
		if (string.IsNullOrWhiteSpace(credential?.ClientCode) && string.IsNullOrWhiteSpace(credential?.Username))
		{
			missing.Add("Root.ClientCode");
		}
		if (string.IsNullOrWhiteSpace(credential?.Password) && string.IsNullOrWhiteSpace(ProviderBlueprintBase.GetAdditionalSetting(credential, "panelPassword")))
		{
			missing.Add("Root.Password");
		}

		SuratGonderiPayload payload = SuratCargoProvider.BuildGonderi(shipment, credential);
		if (payload.ValidationError is not null)
		{
			missing.Add(payload.ValidationError);
		}

		var preview = new
		{
			restCreate = "POST /api/GonderiyiKargoyaGonder/GonderiBarkodOlustur?CariKodu=&Sifre=",
			soapCreate = "GonderiyiKargoyaGonderYeni",
			restTrack = "POST /api/KargoTakipHareketDetayi?CariKodu=&Sifre=&WebSiparisKodu=",
			credentials = new
			{
				cariKodu = credential?.ClientCode ?? credential?.Username,
				sifre = ProviderBlueprintBase.MaskSecret(credential?.Password ?? ProviderBlueprintBase.GetAdditionalSetting(credential, "panelPassword"))
			},
			gonderi = payload.Body
		};

		return ProviderBlueprintBase.BuildPreview(
			SupportedProvider,
			shipment.ShipmentReference,
			payload.OzelKargoTakipNo,
			preview,
			missing,
			null,
			new[]
			{
				"WebSiparisKodu olarak OzelKargoTakipNo (ShipmentReference) kullanılır.",
				"SOAP başarı dönüşü Tamam veya 013-016 barkod mesajlarıdır.",
				"Takip yanıtındaki KargonunDurumuSayi 1-16 kodları Studio durumuna map edilir."
			},
			liveTransportImplemented: true);
	}
}
