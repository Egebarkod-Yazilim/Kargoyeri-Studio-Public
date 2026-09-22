using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal static class LegacyProviderMappingHelpers
{
	public static string NormalizePhone(string? phoneNumber, int desiredLength = 10)
	{
		if (string.IsNullOrWhiteSpace(phoneNumber))
		{
			return string.Empty;
		}
		string text = new string(phoneNumber.Where(char.IsDigit).ToArray());
		if (text.Length <= desiredLength)
		{
			return text;
		}
		int length = text.Length;
		int num = length - desiredLength;
		return text.Substring(num, length - num);
	}

	public static string GenerateNumericCode(int length)
	{
		char[] array = new char[length];
		for (int i = 0; i < length; i++)
		{
			array[i] = (char)(48 + RandomNumberGenerator.GetInt32(0, 10));
		}
		return new string(array);
	}

	public static string ResolveRecipientName(CargoShipment shipment)
	{
		if (!string.IsNullOrWhiteSpace(shipment.Recipient.Name))
		{
			return shipment.Recipient.Name;
		}
		return shipment.Recipient.CompanyName ?? shipment.OrderReference;
	}

	public static string ResolveAddress(AddressInfo info)
	{
		return string.Join(" ", new string[2] { info.AddressLine1, info.AddressLine2 }.Where((string x) => !string.IsNullOrWhiteSpace(x))).Trim();
	}

	public static string ResolveCity(AddressInfo info)
	{
		return (!string.IsNullOrWhiteSpace(info.City)) ? info.City : info.CountryCode;
	}

	public static string ResolveDistrict(AddressInfo info)
	{
		return (!string.IsNullOrWhiteSpace(info.District)) ? info.District : info.City;
	}

	public static string ResolveInvoiceNumber(CargoShipment shipment, string fallbackPrefix, int randomLength)
	{
		string metadataValue = GetMetadataValue(shipment, "invoice.serial", "invoiceSerial");
		string metadataValue2 = GetMetadataValue(shipment, "invoice.sequence", "invoiceSequence");
		if (!string.IsNullOrWhiteSpace(metadataValue) && !string.IsNullOrWhiteSpace(metadataValue2))
		{
			return metadataValue + metadataValue2;
		}
		return fallbackPrefix + GenerateNumericCode(randomLength);
	}

	public static LegacyPaymentKind ResolvePaymentKind(CargoShipment shipment)
	{
		string metadataValue = GetMetadataValue(shipment, "payment.type", "paymentType", "payment_method", "paymentMethodSystemName");
		if (!string.IsNullOrWhiteSpace(metadataValue))
		{
			string text = metadataValue.Trim().ToLowerInvariant();
			bool flag;
			switch (text)
			{
			case "payments.cashondelivery":
			case "cod_cash":
			case "cashondelivery":
			case "kapidanakit":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				return LegacyPaymentKind.CashOnDeliveryCash;
			}
			switch (text)
			{
			case "payments.purchaseorder":
			case "cod_card":
			case "purchaseorder":
			case "kapidakart":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				return LegacyPaymentKind.CashOnDeliveryCard;
			}
		}
		return (shipment.CollectionAmount.GetValueOrDefault() > 0m) ? LegacyPaymentKind.CashOnDeliveryCash : LegacyPaymentKind.Prepaid;
	}

	public static bool RecipientPaysShipping(CargoShipment shipment)
	{
		string metadataValue = GetMetadataValue(shipment, "shipping.payor", "payorType");
		if (!string.IsNullOrWhiteSpace(metadataValue))
		{
			bool result;
			switch (metadataValue.Trim().ToLowerInvariant())
			{
			case "recipient":
			case "consignee":
			case "receiver":
			case "alici":
			case "buyer":
				result = true;
				break;
			default:
				result = false;
				break;
			}
			return result;
		}
		string metadataValue2 = GetMetadataValue(shipment, "recipientPaysShipping", "buyerPaysShipping");
		return IsTrue(metadataValue2);
	}

	public static int GetPackageCount(CargoShipment shipment)
	{
		return Math.Max(1, shipment.Packages.Count);
	}

	public static decimal ResolveCollectionAmount(CargoShipment shipment)
	{
		if (shipment.CollectionAmount.HasValue && shipment.CollectionAmount.Value > 0m)
		{
			return shipment.CollectionAmount.Value;
		}
		return shipment.Packages.Sum((PackageInfo x) => x.CashOnDeliveryAmount.GetValueOrDefault());
	}

	public static decimal SumWeight(CargoShipment shipment, decimal fallbackPerPackage = 1m)
	{
		decimal num = shipment.Packages.Sum((PackageInfo x) => x.Weight);
		if (num > 0m)
		{
			return num;
		}
		return fallbackPerPackage * (decimal)GetPackageCount(shipment);
	}

	public static decimal SumDesi(CargoShipment shipment, decimal fallbackPerPackage = 1m)
	{
		decimal num = shipment.Packages.Sum((PackageInfo x) => x.Desi);
		if (num > 0m)
		{
			return num;
		}
		return fallbackPerPackage * (decimal)GetPackageCount(shipment);
	}

	public static string BuildPackageContent(CargoShipment shipment)
	{
		PackageInfo[] source = ((shipment.Packages.Count != 0) ? shipment.Packages.OrderBy((PackageInfo x) => x.PackageSequence).ToArray() : new PackageInfo[1]
		{
			new PackageInfo
			{
				PackageSequence = 1,
				Description = "Urun"
			}
		});
		return string.Join(string.Empty, source.Select(delegate(PackageInfo package, int index)
		{
			string value = (string.IsNullOrWhiteSpace(package.Description) ? "Urun" : package.Description);
			decimal value2 = ((package.Weight > 0m) ? package.Weight : 1m);
			decimal value3 = ((package.Desi > 0m) ? package.Desi : 1m);
			int value4 = ((package.PackageSequence > 0) ? package.PackageSequence : (index + 1));
			return $"{value2} : {value3} : {value3} : {value} :{value4}:;";
		}));
	}

	public static string? GetMetadataValue(CargoShipment shipment, params string[] keys)
	{
		foreach (string b in keys)
		{
			foreach (KeyValuePair<string, string> metadatum in shipment.Metadata)
			{
				if (string.Equals(metadatum.Key, b, StringComparison.OrdinalIgnoreCase))
				{
					return metadatum.Value;
				}
			}
		}
		return null;
	}

	public static int? GetIntMetadata(CargoShipment shipment, params string[] keys)
	{
		string metadataValue = GetMetadataValue(shipment, keys);
		int result;
		return int.TryParse(metadataValue, out result) ? new int?(result) : null;
	}

	public static bool IsTrue(string? value)
	{
		return value != null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
	}
}
