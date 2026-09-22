using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class PttProviderBlueprint : ProviderBlueprintBase
{
	public override CargoProviderType SupportedProvider => CargoProviderType.Ptt;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "Ptt",
			Provider = "PTT",
			IntegrationStyle = "SOAP kabulEkle2 operasyonu",
			AuthenticationStyle = "kullanici/sifre + musteriId SOAP gövdesinde",
			RecommendedIntegrationMode = "Gerçek SOAP transport: pttws.ptt.gov.tr (EndpointBase ile ayarlanır)",
			PublicDocsAvailable = false,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			LegacySource = "KargoEntegre/CargoProcedures/PTT/PTTSiparis.cs",
			SourceNote = "Legacy code posts kabulEkle2 and increments a barcode number locally before mapping the recipient fields.",
			OfficialDocsSummary = "Public technical integration documentation could not be verified from official sources during the latest check.",
			LastVerifiedDate = "2026-04-09",
			SupportedOperations = new List<string> { "CreateShipment" },
			ModernizationNotes = new List<string> { "Barcode seed handling should be moved to a safer sequential allocator before a live transport is written.", "PTT transport should only be completed after current official service contract is confirmed." },
			MetadataHints = new List<string> { "payment.type or paymentMethodSystemName" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("Username", "WS Username", required: true, secret: false, "PTT integration username."),
				ProviderBlueprintBase.RootField("Password", "WS Password", required: true, secret: true, "PTT integration password."),
				ProviderBlueprintBase.AdditionalField("customerId", "Customer Id", required: true, secret: false, "PTT customer id.", "1001"),
				ProviderBlueprintBase.AdditionalField("barcodeSeed", "Barcode Seed", required: true, secret: false, "Current numeric barcode seed stored per tenant.", "123456789012")
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
		string additionalSetting = ProviderBlueprintBase.GetAdditionalSetting(credential, "customerId");
		if (string.IsNullOrWhiteSpace(additionalSetting))
		{
			list.Add("AdditionalSettings.customerId");
		}
		string additionalSetting2 = ProviderBlueprintBase.GetAdditionalSetting(credential, "barcodeSeed");
		if (string.IsNullOrWhiteSpace(additionalSetting2))
		{
			list.Add("AdditionalSettings.barcodeSeed");
		}
		long.TryParse(additionalSetting2, out var result);
		string text = ((result > 0) ? CalculateCheckDigit(result + 1).ToString() : null);
		var payload = new
		{
			service = "kabulEkle2",
			input = new
			{
				musteriId = ParseInt(additionalSetting),
				gonderiTur = "KARGO",
				gonderiTip = "NORMAL",
				kullanici = credential?.Username,
				sifre = ProviderBlueprintBase.MaskSecret(credential?.Password),
				dongu = new[]
				{
					new
					{
						aAdres = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient),
						aliciAdi = LegacyProviderMappingHelpers.ResolveRecipientName(shipment),
						aliciIlAdi = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient),
						aliciIlceAdi = LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient),
						aliciSms = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone),
						aliciTel = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone),
						barkodNo = text,
						odemesekli = "N"
					}
				}
			}
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, text, payload, list, null, new string[2] { "The legacy PTT implementation appears to persist only a derived check-digit barcode value; treat barcode generation as a live-integration hardening task.", "Cancel flow was not implemented in the source project and remains open in the API model." });
	}

	private static int? ParseInt(string? value)
	{
		int result;
		return int.TryParse(value, out result) ? new int?(result) : null;
	}

	private static int CalculateCheckDigit(long number)
	{
		int num = 0;
		for (int i = 0; i < 12; i++)
		{
			if (i % 2 == 0)
			{
				int num2 = (int)(number % 10);
				num += ((i % 4 == 0) ? num2 : (num2 * 3));
			}
			number /= 10;
		}
		return (10 - num % 10) % 10;
	}
}
