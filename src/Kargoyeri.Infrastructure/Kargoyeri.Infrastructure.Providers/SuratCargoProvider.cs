using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Infrastructure.Providers.Blueprints;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class SuratCargoProvider : ICargoProvider
{
	private const string DefaultBaseUrl = "https://api02.suratkargo.com.tr";

	private readonly IEnumerable<IProviderBlueprint> _blueprints;

	private readonly IHttpClientFactory _httpClientFactory;

	private readonly ILogger<SuratCargoProvider> _logger;

	public CargoProviderType SupportedProvider => CargoProviderType.Surat;

	public SuratCargoProvider(IEnumerable<IProviderBlueprint> blueprints, IHttpClientFactory httpClientFactory, ILogger<SuratCargoProvider> logger)
	{
		_blueprints = blueprints;
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public async Task<ProviderShipmentResult> CreateShipmentAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			return SimulateCreate(shipment, credential);
		}
		string baseUrl = ResolveBaseUrl(credential);
		LegacyPaymentKind paymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		bool isCod = paymentKind != LegacyPaymentKind.Prepaid;
		string userName = (isCod ? (GetSetting(credential, "usernameCod") ?? credential?.Username) : credential?.Username);
		string panelPassword = (isCod ? (GetSetting(credential, "panelPasswordCod") ?? GetSetting(credential, "panelPassword")) : GetSetting(credential, "panelPassword"));
		if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(panelPassword))
		{
			return ProviderShipmentResult.Fail("Sürat Kargo kimlik bilgileri eksik (Username veya panelPassword).", "{\"error\":\"missing_credentials\"}");
		}
		string tracking = LegacyProviderMappingHelpers.GenerateNumericCode(13);
		int packageCount = LegacyProviderMappingHelpers.GetPackageCount(shipment);
		string aliciAdresi = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient);
		string email = shipment.Recipient.Email ?? string.Empty;
		string il = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient);
		string ilce = LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient);
		string telefonCep = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone);
		decimal birimDesi = LegacyProviderMappingHelpers.SumDesi(shipment);
		decimal birimKg = LegacyProviderMappingHelpers.SumWeight(shipment);
		string ozelKargoTakipNo = tracking;
		byte valueOrDefault = ParseByte(GetSetting(credential, "packageType")).GetValueOrDefault(2);
		int odemetipi = ((!LegacyProviderMappingHelpers.RecipientPaysShipping(shipment)) ? 1 : 2);
		string kisiKurum = LegacyProviderMappingHelpers.ResolveRecipientName(shipment);
		string metadataValue = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "invoice.serial", "invoiceSerial");
		string metadataValue2 = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "invoice.sequence", "invoiceSequence");
		decimal? kapidanOdemeTutari = (isCod ? new decimal?(LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment)) : null);
		if (1 == 0)
		{
		}
		int kapidanOdemeTahsilatTipi = paymentKind switch
		{
			LegacyPaymentKind.CashOnDeliveryCash => 1, 
			LegacyPaymentKind.CashOnDeliveryCard => 2, 
			_ => 0, 
		};
		if (1 == 0)
		{
		}
		var gonderi = new
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
			KargoTuru = valueOrDefault,
			Odemetipi = odemetipi,
			TeslimSekli = 1,
			KisiKurum = kisiKurum,
			IrsaliyeSeriNo = metadataValue,
			IrsaliyeSiraNo = metadataValue2,
			KapidanOdemeTutari = kapidanOdemeTutari,
			KapidanOdemeTahsilatTipi = kapidanOdemeTahsilatTipi
		};
		string url = $"{baseUrl}/api/GonderiyiKargoyaGonder/GonderiBarkodOlustur?CariKodu={Uri.EscapeDataString(userName)}&Sifre={Uri.EscapeDataString(panelPassword)}";
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			string json = JsonSerializer.Serialize(gonderi);
			using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await http.PostAsync(url, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Sürat Kargo gonderi olusturma basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"Sürat Kargo API hatasi (HTTP {response.StatusCode}).", raw);
			}
			string error = ExtractErrorMessage(raw);
			if (error != null)
			{
				_logger.LogWarning("Sürat Kargo API hata dondu {Ref}: {Error}", shipment.ShipmentReference, error);
				return ProviderShipmentResult.Fail("Sürat Kargo hatasi: " + error, raw);
			}
			string finalTracking = ExtractTrackingNumber(raw) ?? tracking;
			_logger.LogInformation("Sürat Kargo gonderisi olusturuldu {Ref}. Takip: {Tracking}", shipment.ShipmentReference, finalTracking);
			return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, finalTracking, null, null, "Surat Kargo gonderisi olusturuldu.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "Sürat Kargo gonderi istegi hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Surat Kargo baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			_logger.LogInformation("Surat Kargo simulasyon iptali {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, shipment.LabelUrl, shipment.LabelContentBase64, string.IsNullOrWhiteSpace(reason) ? "Surat Kargo simulasyon iptali." : ("Surat Kargo simulasyon iptali: " + reason), "{\"mode\":\"simulation\",\"operation\":\"cancel\"}");
		}
		string baseUrl = ResolveBaseUrl(credential);
		string userName = credential?.Username;
		string password = credential?.Password;
		if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("Surat Kargo kimlik bilgileri eksik (Username veya Password).", "{\"error\":\"missing_credentials\"}");
		}
		string webOrderCode = shipment.TrackingNumber ?? shipment.ShipmentReference;
		string url = $"{baseUrl}/api/Gonderi/GonderiSil?CariKodu={Uri.EscapeDataString(userName)}&Sifre={Uri.EscapeDataString(password)}&WebSiparisKodu={Uri.EscapeDataString(webOrderCode)}";
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using HttpResponseMessage response = await http.PostAsync(url, null, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Surat Kargo iptal basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"Surat Kargo iptal API hatasi (HTTP {response.StatusCode}).", raw);
			}
			string error = ExtractErrorMessage(raw);
			if (error != null)
			{
				return ProviderShipmentResult.Fail("Surat Kargo iptal hatasi: " + error, raw);
			}
			_logger.LogInformation("Surat Kargo gonderisi iptal edildi {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, string.IsNullOrWhiteSpace(reason) ? "Surat Kargo gonderisi iptal edildi." : ("Surat Kargo iptal: " + reason), raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "Surat Kargo iptal istegi hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Surat Kargo iptal baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> RefreshStatusAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			ShipmentStatus status2 = shipment.Status;
			if (1 == 0)
			{
			}
			ShipmentStatus shipmentStatus;
			switch (status2)
			{
			case ShipmentStatus.Pending:
				shipmentStatus = ShipmentStatus.ProviderAccepted;
				break;
			case ShipmentStatus.ProviderAccepted:
				shipmentStatus = ShipmentStatus.InTransit;
				break;
			case ShipmentStatus.InTransit:
				if (shipment.RetryCount < 2)
				{
					goto default;
				}
				shipmentStatus = ShipmentStatus.Delivered;
				break;
			default:
				shipmentStatus = shipment.Status;
				break;
			}
			if (1 == 0)
			{
			}
			ShipmentStatus nextSim = shipmentStatus;
			_logger.LogInformation("Surat Kargo simulasyon durum guncelleme {Ref}: {Status}", shipment.ShipmentReference, nextSim);
			return ProviderShipmentResult.Ok(nextSim, shipment.TrackingNumber, null, null, $"Surat Kargo simulasyon durumu: {nextSim}.", $"{{\"mode\":\"simulation\",\"status\":\"{nextSim}\"}}");
		}
		string baseUrl = ResolveBaseUrl(credential);
		string userName = credential?.Username;
		string password = credential?.Password;
		string webOrderCode = shipment.TrackingNumber ?? shipment.ShipmentReference;
		if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("Surat Kargo kimlik bilgileri eksik.");
		}
		string url = $"{baseUrl}/api/KargoTakipHareket/KargoTakipHareketDetayi?CariKodu={Uri.EscapeDataString(userName)}&Sifre={Uri.EscapeDataString(password)}&WebSiparisKodu={Uri.EscapeDataString(webOrderCode)}";
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using HttpResponseMessage response = await http.PostAsync(url, null, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return ProviderShipmentResult.Fail($"Surat Kargo durum sorgu hatasi (HTTP {response.StatusCode}).", raw);
			}
			ShipmentStatus status = MapSuratStatus(raw, shipment.Status);
			_logger.LogInformation("Surat Kargo durum guncellendi {Ref}: {Status}", shipment.ShipmentReference, status);
			return ProviderShipmentResult.Ok(status, shipment.TrackingNumber, null, null, $"Surat Kargo durum: {status}.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "Surat Kargo durum istegi hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Surat Kargo durum baglanti hatasi: " + ex.Message);
		}
	}

	private static string ResolveBaseUrl(ProviderCredential? credential)
	{
		return (credential?.EndpointBase ?? "https://api02.suratkargo.com.tr").TrimEnd('/');
	}

	private static bool IsSimulation(ProviderCredential? credential)
	{
		return IsTrue(GetSetting(credential, "simulationMode"));
	}

	private ProviderShipmentResult SimulateCreate(CargoShipment shipment, ProviderCredential? credential)
	{
		ProviderRequestPreviewResponse providerRequestPreviewResponse = _blueprints.FirstOrDefault((IProviderBlueprint x) => x.SupportedProvider == CargoProviderType.Surat)?.PreviewCreateShipment(shipment, credential);
		object obj = providerRequestPreviewResponse?.TrackingNumberCandidate;
		if (obj == null)
		{
			string shipmentReference = shipment.ShipmentReference;
			int length = shipmentReference.Length;
			int num = length - 8;
			obj = "SIM-" + shipmentReference.Substring(num, length - num);
		}
		string text = (string)obj;
		_logger.LogInformation("Surat Kargo simulasyon gonderisi olusturuldu {Ref}. Takip: {Tracking}", shipment.ShipmentReference, text);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text, null, null, "Surat Kargo simulasyon gonderisi olusturuldu.", providerRequestPreviewResponse?.PayloadPreview);
	}

	private static string? GetSetting(ProviderCredential? credential, string key)
	{
		if (credential?.AdditionalSettings == null)
		{
			return null;
		}
		foreach (KeyValuePair<string, string> additionalSetting in credential.AdditionalSettings)
		{
			if (string.Equals(additionalSetting.Key, key, StringComparison.OrdinalIgnoreCase))
			{
				return additionalSetting.Value;
			}
		}
		return null;
	}

	private static bool IsTrue(string? value)
	{
		return value != null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase));
	}

	private static byte? ParseByte(string? value)
	{
		byte result;
		return byte.TryParse(value, out result) ? new byte?(result) : null;
	}

	private static string? ExtractErrorMessage(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement rootElement = jsonDocument.RootElement;
			string[] array = new string[8] { "ErrorMessage", "errorMessage", "Message", "message", "Hata", "hata", "Error", "error" };
			foreach (string propertyName in array)
			{
				if (rootElement.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
				{
					string @string = value.GetString();
					if (!string.IsNullOrWhiteSpace(@string))
					{
						return @string;
					}
				}
			}
			string[] array2 = new string[4] { "IsSuccess", "isSuccess", "Success", "success" };
			foreach (string propertyName2 in array2)
			{
				if (rootElement.TryGetProperty(propertyName2, out var value2) && value2.ValueKind == JsonValueKind.False)
				{
					return "Islem basarisiz (provider success=false).";
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static string? ExtractTrackingNumber(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement rootElement = jsonDocument.RootElement;
			string[] array = new string[10] { "KargoNo", "kargoNo", "BarkodNo", "barkodNo", "TrackingNumber", "trackingNumber", "BarcodeNumber", "barcodeNumber", "OzelKargoTakipNo", "ozelKargoTakipNo" };
			foreach (string propertyName in array)
			{
				if (rootElement.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
				{
					string @string = value.GetString();
					if (!string.IsNullOrWhiteSpace(@string))
					{
						return @string;
					}
				}
			}
			if (rootElement.TryGetProperty("Data", out var value2) || rootElement.TryGetProperty("data", out value2))
			{
				return ExtractTrackingNumber(value2.GetRawText());
			}
		}
		catch
		{
		}
		return null;
	}

	private static ShipmentStatus MapSuratStatus(string? raw, ShipmentStatus current)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return current;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement rootElement = jsonDocument.RootElement;
			string[] array = new string[6] { "Durum", "durum", "Status", "status", "KargoDurum", "LastStatus" };
			foreach (string propertyName in array)
			{
				if (rootElement.TryGetProperty(propertyName, out var value))
				{
					string text = ((value.ValueKind == JsonValueKind.String) ? value.GetString() : value.ToString())?.ToLowerInvariant() ?? string.Empty;
					if (text.Contains("teslim edildi") || text.Contains("delivered"))
					{
						return ShipmentStatus.Delivered;
					}
					if (text.Contains("dagitim") || text.Contains("yolda") || text.Contains("transit") || text.Contains("hareket"))
					{
						return ShipmentStatus.InTransit;
					}
					if (text.Contains("kabul") || text.Contains("alindi") || text.Contains("accepted"))
					{
						return ShipmentStatus.ProviderAccepted;
					}
					if (text.Contains("iptal") || text.Contains("cancel"))
					{
						return ShipmentStatus.Cancelled;
					}
				}
			}
			if (rootElement.TryGetProperty("Data", out var value2) || rootElement.TryGetProperty("data", out value2))
			{
				return MapSuratStatus(value2.GetRawText(), current);
			}
		}
		catch
		{
		}
		return current;
	}
}
