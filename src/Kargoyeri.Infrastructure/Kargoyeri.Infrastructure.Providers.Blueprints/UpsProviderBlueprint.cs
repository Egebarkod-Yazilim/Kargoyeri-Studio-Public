using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal sealed class UpsProviderBlueprint : ProviderBlueprintBase
{
	private static readonly IReadOnlyDictionary<int, int> CountyCodeFixes = new Dictionary<int, int>
	{
		[9999] = 136,
		[9998] = 136,
		[9997] = 195,
		[9996] = 113,
		[9995] = 264,
		[9994] = 5763,
		[9993] = 328,
		[9992] = 328,
		[9991] = 644,
		[9990] = 644,
		[9989] = 447,
		[9988] = 553,
		[9987] = 626,
		[9986] = 626,
		[9985] = 707,
		[9984] = 654,
		[9983] = 741,
		[9982] = 664,
		[9981] = 857,
		[9980] = 857,
		[9979] = 805,
		[9978] = 805,
		[9977] = 828,
		[9976] = 874,
		[9975] = 874
	};

	public override CargoProviderType SupportedProvider => CargoProviderType.Ups;

	public override ProviderProfileDto Describe()
	{
		return new ProviderProfileDto
		{
			ProviderCode = "Ups",
			Provider = "UPS",
			IntegrationStyle = "REST OAuth2 Ship API + Track API",
			AuthenticationStyle = "OAuth2 client_credentials: Username=ClientId, ApiKey=ClientSecret",
			RecommendedIntegrationMode = "Gerçek REST transport: onlinetools.ups.com/api/shipments + /api/track",
			PublicDocsAvailable = true,
			LiveTransportImplemented = true,
			RequestPreviewAvailable = true,
			LegacySource = "KargoEntegre/CargoProcedures/UPS/UPSCreateShipment.cs",
			SourceNote = "Legacy code logs in for a session and posts CreateShipment_Type2 with shipment info and label output.",
			SourceUrl = "https://developer.ups.com/",
			OfficialDocsSummary = "UPS developer portal centers API catalog access and OAuth-based authorization. Legacy SOAP in the old project should be treated as compatibility mode.",
			LastVerifiedDate = "2026-04-09",
			SupportedOperations = new List<string> { "CreateShipment", "CancelShipment" },
			KnownApiProducts = new List<string> { "OAuth Authorization", "Developer Portal API Catalog" },
			KnownEndpointHints = new List<string> { "Official portal emphasizes OAuth and current certificates.", "Legacy SOAP create/query code remains useful for request mapping but should not be the long-term target." },
			ModernizationNotes = new List<string> { "New UPS transport should be designed around OAuth client credentials/portal app registration.", "Label and tracking operations should be separated from old disk-based file persistence." },
			MetadataHints = new List<string> { "payment.type or paymentMethodSystemName", "shipping.payor", "ups.recipientCityCode", "ups.recipientAreaCode", "invoice.serial and invoice.sequence" },
			SettingsSchema = new List<ProviderSettingFieldDto>
			{
				ProviderBlueprintBase.RootField("ClientCode", "Customer Number", required: true, secret: false, "UPS shipper account / customer number."),
				ProviderBlueprintBase.RootField("Username", "WS Username", required: true, secret: false, "UPS integration username."),
				ProviderBlueprintBase.RootField("Password", "WS Password", required: true, secret: true, "UPS integration password."),
				ProviderBlueprintBase.RootField("ApiKey", "OAuth Client Id / API Key", required: false, secret: true, "Portal app client id or API key for modern UPS integration."),
				ProviderBlueprintBase.AdditionalField("companyName", "Company Name", required: true, secret: false, "Sender company name used as shipper."),
				ProviderBlueprintBase.AdditionalField("companyAddress", "Company Address", required: true, secret: false, "Sender address used as shipper."),
				ProviderBlueprintBase.AdditionalField("cityCode", "Shipper City Code", required: true, secret: false, "UPS numeric sender city code.", "34"),
				ProviderBlueprintBase.AdditionalField("countyCode", "Shipper County Code", required: true, secret: false, "UPS numeric sender area code.", "34"),
				ProviderBlueprintBase.AdditionalField("packageType", "Package Type", required: true, secret: false, "Legacy package type code.", "K"),
				ProviderBlueprintBase.AdditionalField("integrationMode", "Integration Mode", required: false, secret: false, "Suggested values: LegacySoap, RestOAuth", "RestOAuth"),
				ProviderBlueprintBase.AdditionalField("oauthClientSecret", "OAuth Client Secret", required: false, secret: true, "Client secret for modern UPS app registration.")
			}
		};
	}

	public override ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential)
	{
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		RequireRoot(credential, "ClientCode", list);
		RequireRoot(credential, "Username", list);
		RequireRoot(credential, "Password", list);
		RequireAdditional(credential, "companyName", list);
		RequireAdditional(credential, "companyAddress", list);
		RequireAdditional(credential, "cityCode", list);
		RequireAdditional(credential, "countyCode", list);
		RequireAdditional(credential, "packageType", list);
		int? intMetadata = LegacyProviderMappingHelpers.GetIntMetadata(shipment, "ups.recipientCityCode");
		if (!intMetadata.HasValue)
		{
			list2.Add("ups.recipientCityCode");
		}
		int? intMetadata2 = LegacyProviderMappingHelpers.GetIntMetadata(shipment, "ups.recipientAreaCode");
		if (!intMetadata2.HasValue)
		{
			list2.Add("ups.recipientAreaCode");
		}
		LegacyPaymentKind legacyPaymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		string text = LegacyProviderMappingHelpers.ResolveInvoiceNumber(shipment, "GE", 10);
		var login = new
		{
			customerNumber = credential?.ClientCode,
			userName = credential?.Username,
			password = ProviderBlueprintBase.MaskSecret(credential?.Password)
		};
		string additionalSetting = ProviderBlueprintBase.GetAdditionalSetting(credential, "companyName");
		string additionalSetting2 = ProviderBlueprintBase.GetAdditionalSetting(credential, "companyAddress");
		int? shipperCityCode = ParseInt(ProviderBlueprintBase.GetAdditionalSetting(credential, "cityCode"));
		int? shipperAreaCode = ParseInt(ProviderBlueprintBase.GetAdditionalSetting(credential, "countyCode"));
		string shipperAccountNumber = credential?.ClientCode;
		string consigneeName = LegacyProviderMappingHelpers.ResolveRecipientName(shipment);
		string consigneeAddress = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient);
		int? consigneeCityCode = intMetadata;
		int? consigneeAreaCode = NormalizeCountyCode(intMetadata2);
		string consigneePhoneNumber = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone);
		string email = shipment.Recipient.Email;
		int paymentType = (LegacyProviderMappingHelpers.RecipientPaysShipping(shipment) ? 1 : 2);
		string additionalSetting3 = ProviderBlueprintBase.GetAdditionalSetting(credential, "packageType");
		int packageCount = LegacyProviderMappingHelpers.GetPackageCount(shipment);
		string customerInvoiceNumber = text;
		string orderReference = shipment.OrderReference;
		decimal? valueOfGoods = ((legacyPaymentKind == LegacyPaymentKind.Prepaid) ? null : new decimal?(LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment)));
		string valueOfGoodsCurrency = ((legacyPaymentKind == LegacyPaymentKind.Prepaid) ? null : shipment.CurrencyCode);
		if (1 == 0)
		{
		}
		int valueOfGoodsPaymentType = legacyPaymentKind switch
		{
			LegacyPaymentKind.CashOnDeliveryCash => 1, 
			LegacyPaymentKind.CashOnDeliveryCard => 3, 
			_ => 0, 
		};
		if (1 == 0)
		{
		}
		var payload = new
		{
			login = login,
			service = "CreateShipment_Type2",
			shipmentInfo = new
			{
				ShipperName = additionalSetting,
				ShipperAddress = additionalSetting2,
				ShipperCityCode = shipperCityCode,
				ShipperAreaCode = shipperAreaCode,
				ShipperAccountNumber = shipperAccountNumber,
				ConsigneeName = consigneeName,
				ConsigneeAddress = consigneeAddress,
				ConsigneeCityCode = consigneeCityCode,
				ConsigneeAreaCode = consigneeAreaCode,
				ConsigneePhoneNumber = consigneePhoneNumber,
				ConsigneeEMail = email,
				ServiceLevel = 3,
				PaymentType = paymentType,
				PackageType = additionalSetting3,
				NumberOfPackages = packageCount,
				CustomerInvoiceNumber = customerInvoiceNumber,
				CustomerReferance = orderReference,
				ValueOfGoods = valueOfGoods,
				ValueOfGoodsCurrency = valueOfGoodsCurrency,
				ValueOfGoodsPaymentType = valueOfGoodsPaymentType
			}
		};
		return ProviderBlueprintBase.BuildPreview(SupportedProvider, shipment.ShipmentReference, text, payload, list, list2, new string[2] { "Recipient numeric city/area codes should be supplied by the caller because the API model stores normalized text addresses.", "The legacy project persisted label files on disk; Kargoyeri keeps label handling abstract for shared API usage." });
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

	private static int? ParseInt(string? value)
	{
		int result;
		return int.TryParse(value, out result) ? new int?(result) : null;
	}

	private static int? NormalizeCountyCode(int? countyCode)
	{
		if (!countyCode.HasValue)
		{
			return null;
		}
		int value;
		return CountyCodeFixes.TryGetValue(countyCode.Value, out value) ? value : countyCode.Value;
	}
}
