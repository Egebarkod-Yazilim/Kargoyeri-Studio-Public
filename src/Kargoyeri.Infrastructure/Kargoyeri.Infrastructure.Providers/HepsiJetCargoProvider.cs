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

internal sealed class HepsiJetCargoProvider : ICargoProvider
{
	private const string DefaultBaseUrl = "https://integration-api.hepsijet.com";

	private readonly IEnumerable<IProviderBlueprint> _blueprints;

	private readonly IHttpClientFactory _httpClientFactory;

	private readonly ILogger<HepsiJetCargoProvider> _logger;

	public CargoProviderType SupportedProvider => CargoProviderType.HepsiJet;

	public HepsiJetCargoProvider(IEnumerable<IProviderBlueprint> blueprints, IHttpClientFactory httpClientFactory, ILogger<HepsiJetCargoProvider> logger)
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
		string token = await GetTokenAsync(credential, cancellationToken);
		if (token == null)
		{
			return ProviderShipmentResult.Fail("HepsiJet token alinamadi. Kimlik bilgilerini kontrol edin.", "{\"error\":\"token_failed\"}");
		}
		string baseUrl = ResolveBaseUrl(credential);
		string barcode = "HJ-" + LegacyProviderMappingHelpers.GenerateNumericCode(10);
		LegacyPaymentKind paymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		string deliveryType = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "hepsijet.deliveryType", "deliveryType") ?? "STANDARD";
		int pc;
		int parcelCount = (int.TryParse(LegacyProviderMappingHelpers.GetMetadataValue(shipment, "hepsijet.parcelCount", "parcelCount"), out pc) ? pc : LegacyProviderMappingHelpers.GetPackageCount(shipment));
		var company = new
		{
			name = (GetSetting(credential, "companyName") ?? "HepsiJET")
		};
		string barcode2 = barcode;
		int totalParcel = parcelCount;
		string deliveryType2 = deliveryType;
		if (1 == 0)
		{
		}
		string paymentType = paymentKind switch
		{
			LegacyPaymentKind.CashOnDeliveryCash => "CASH_ON_DELIVERY", 
			LegacyPaymentKind.CashOnDeliveryCard => "CARD_ON_DELIVERY", 
			_ => "PAID", 
		};
		if (1 == 0)
		{
		}
		var order = new
		{
			company = company,
			barcode = barcode2,
			totalParcel = totalParcel,
			deliveryType = deliveryType2,
			cargo = new
			{
				paymentType = paymentType,
				amount = LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment)
			},
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
			}
		};
		string url = baseUrl + "/sendDeliveryOrderEnhanced";
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			http.DefaultRequestHeaders.TryAddWithoutValidation("X-Auth-Token", token);
			string body = JsonSerializer.Serialize(new
			{
				orders = new[] { order }
			});
			using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await http.PostAsync(url, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("HepsiJet gonderi olusturma basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"HepsiJet API hatasi (HTTP {response.StatusCode}).", raw);
			}
			string error = ExtractError(raw);
			if (error != null)
			{
				_logger.LogWarning("HepsiJet API hata dondu {Ref}: {Error}", shipment.ShipmentReference, error);
				return ProviderShipmentResult.Fail("HepsiJet hatasi: " + error, raw);
			}
			string finalBarcode = ExtractBarcode(raw) ?? barcode;
			_logger.LogInformation("HepsiJet gonderisi olusturuldu {Ref}. Barkod: {Barcode}", shipment.ShipmentReference, finalBarcode);
			return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, finalBarcode, null, null, "HepsiJet gonderisi olusturuldu.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "HepsiJet gonderi istegi hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("HepsiJet baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			_logger.LogInformation("HepsiJet simulasyon iptali {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, shipment.LabelUrl, shipment.LabelContentBase64, string.IsNullOrWhiteSpace(reason) ? "HepsiJet simulasyon iptali." : ("HepsiJet simulasyon iptali: " + reason), "{\"mode\":\"simulation\",\"operation\":\"cancel\"}");
		}
		string token = await GetTokenAsync(credential, cancellationToken);
		if (token == null)
		{
			return ProviderShipmentResult.Fail("HepsiJet token alinamadi.");
		}
		string baseUrl = ResolveBaseUrl(credential);
		string deliveryNo = shipment.TrackingNumber ?? shipment.ShipmentReference;
		string url = baseUrl + "/deleteDeliveryOrder/" + Uri.EscapeDataString(deliveryNo);
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			http.DefaultRequestHeaders.TryAddWithoutValidation("X-Auth-Token", token);
			using HttpResponseMessage response = await http.PostAsync(url, null, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("HepsiJet iptal basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"HepsiJet iptal API hatasi (HTTP {response.StatusCode}).", raw);
			}
			string error = ExtractError(raw);
			if (error != null)
			{
				return ProviderShipmentResult.Fail("HepsiJet iptal hatasi: " + error, raw);
			}
			_logger.LogInformation("HepsiJet gonderisi iptal edildi {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, string.IsNullOrWhiteSpace(reason) ? "HepsiJet gonderisi iptal edildi." : ("HepsiJet iptal: " + reason), raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "HepsiJet iptal istegi hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("HepsiJet iptal baglanti hatasi: " + ex.Message);
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
			_logger.LogInformation("HepsiJet simulasyon durum guncelleme {Ref}: {Status}", shipment.ShipmentReference, nextSim);
			return ProviderShipmentResult.Ok(nextSim, shipment.TrackingNumber, null, null, $"HepsiJet simulasyon durumu: {nextSim}.", $"{{\"mode\":\"simulation\",\"status\":\"{nextSim}\"}}");
		}
		string token = await GetTokenAsync(credential, cancellationToken);
		if (token == null)
		{
			return ProviderShipmentResult.Fail("HepsiJet token alinamadi.");
		}
		string baseUrl = ResolveBaseUrl(credential);
		string barcode = shipment.TrackingNumber ?? shipment.ShipmentReference;
		string url = baseUrl + "/delivery/integration/track";
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			http.DefaultRequestHeaders.TryAddWithoutValidation("X-Auth-Token", token);
			string body = JsonSerializer.Serialize(new
			{
				barcodes = new string[1] { barcode },
				isTrackAdded = false
			});
			using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await http.PostAsync(url, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return ProviderShipmentResult.Fail($"HepsiJet durum sorgu hatasi (HTTP {response.StatusCode}).", raw);
			}
			ShipmentStatus status = MapStatus(raw, shipment.Status);
			_logger.LogInformation("HepsiJet durum guncellendi {Ref}: {Status}", shipment.ShipmentReference, status);
			return ProviderShipmentResult.Ok(status, shipment.TrackingNumber, null, null, $"HepsiJet durum: {status}.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "HepsiJet durum istegi hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("HepsiJet durum baglanti hatasi: " + ex.Message);
		}
	}

	private async Task<string?> GetTokenAsync(ProviderCredential? credential, CancellationToken cancellationToken)
	{
		string baseUrl = ResolveBaseUrl(credential);
		string userName = credential?.Username;
		string password = credential?.Password;
		string clientCode = credential?.ClientCode;
		if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
		{
			_logger.LogWarning("HepsiJet: Username veya Password eksik, token istenemez.");
			return null;
		}
		string qs = "?userName=" + Uri.EscapeDataString(userName) + "&password=" + Uri.EscapeDataString(password);
		if (!string.IsNullOrWhiteSpace(clientCode))
		{
			qs = qs + "&clientCode=" + Uri.EscapeDataString(clientCode);
		}
		string tokenUrl = baseUrl + "/auth/getToken" + qs;
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using HttpResponseMessage response = await http.GetAsync(tokenUrl, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("HepsiJet token istegi basarisiz. HTTP {Status}: {Body}", (int)response.StatusCode, raw);
				return null;
			}
			if (response.Headers.TryGetValues("X-Auth-Token", out IEnumerable<string> headerValues))
			{
				string fromHeader = headerValues.FirstOrDefault();
				if (!string.IsNullOrWhiteSpace(fromHeader))
				{
					return fromHeader;
				}
			}
			try
			{
				using JsonDocument doc = JsonDocument.Parse(raw);
				JsonElement root = doc.RootElement;
				string[] array = new string[6] { "token", "Token", "authToken", "AuthToken", "X-Auth-Token", "data" };
				foreach (string field in array)
				{
					if (root.TryGetProperty(field, out var prop) && prop.ValueKind == JsonValueKind.String)
					{
						string val = prop.GetString();
						if (!string.IsNullOrWhiteSpace(val))
						{
							return val;
						}
						prop = default(JsonElement);
					}
				}
			}
			catch
			{
			}
			string trimmed = raw.Trim().Trim('"');
			if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('{') && trimmed.Length >= 8)
			{
				return trimmed;
			}
			_logger.LogWarning("HepsiJet token yaniti anlasilamadi: {Raw}", raw);
			return null;
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "HepsiJet token istegi exception.");
			return null;
		}
	}

	private static string ResolveBaseUrl(ProviderCredential? credential)
	{
		return (credential?.EndpointBase ?? "https://integration-api.hepsijet.com").TrimEnd('/');
	}

	private static bool IsSimulation(ProviderCredential? credential)
	{
		return IsTrue(GetSetting(credential, "simulationMode"));
	}

	private ProviderShipmentResult SimulateCreate(CargoShipment shipment, ProviderCredential? credential)
	{
		ProviderRequestPreviewResponse providerRequestPreviewResponse = _blueprints.FirstOrDefault((IProviderBlueprint x) => x.SupportedProvider == CargoProviderType.HepsiJet)?.PreviewCreateShipment(shipment, credential);
		object obj = providerRequestPreviewResponse?.TrackingNumberCandidate;
		if (obj == null)
		{
			string shipmentReference = shipment.ShipmentReference;
			int length = shipmentReference.Length;
			int num = length - 6;
			obj = "HJ-SIM-" + shipmentReference.Substring(num, length - num);
		}
		string text = (string)obj;
		_logger.LogInformation("HepsiJet simulasyon gonderisi olusturuldu {Ref}. Barkod: {Barcode}", shipment.ShipmentReference, text);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text, null, null, "HepsiJet simulasyon gonderisi olusturuldu.", providerRequestPreviewResponse?.PayloadPreview);
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

	private static string? ExtractError(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement rootElement = jsonDocument.RootElement;
			string[] array = new string[6] { "errorMessage", "ErrorMessage", "message", "Message", "error", "Error" };
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
			if (rootElement.TryGetProperty("status", out var value2) && value2.ValueKind == JsonValueKind.String)
			{
				string string2 = value2.GetString();
				if (!string.IsNullOrWhiteSpace(string2) && !string2.Equals("OK", StringComparison.OrdinalIgnoreCase))
				{
					return "Status: " + string2;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private static string? ExtractBarcode(string? raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.TryGetProperty("data", out var value) && value.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in value.EnumerateArray())
				{
					if (item.TryGetProperty("barcode", out var value2) && value2.ValueKind == JsonValueKind.String)
					{
						string @string = value2.GetString();
						if (!string.IsNullOrWhiteSpace(@string))
						{
							return @string;
						}
					}
				}
			}
			if (rootElement.TryGetProperty("barcode", out var value3) && value3.ValueKind == JsonValueKind.String)
			{
				return value3.GetString();
			}
		}
		catch
		{
		}
		return null;
	}

	private static ShipmentStatus MapStatus(string? raw, ShipmentStatus current)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return current;
		}
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			if (!jsonDocument.RootElement.TryGetProperty("data", out var value) || value.ValueKind != JsonValueKind.Array)
			{
				return current;
			}
			ShipmentStatus? shipmentStatus = null;
			foreach (JsonElement item in value.EnumerateArray())
			{
				if (!item.TryGetProperty("details", out var value2) || value2.ValueKind != JsonValueKind.Array)
				{
					continue;
				}
				foreach (JsonElement item2 in value2.EnumerateArray())
				{
					if (item2.TryGetProperty("integrationStatus", out var value3))
					{
						shipmentStatus = ParseIntegrationStatus(value3.GetString(), current);
					}
				}
			}
			return shipmentStatus.GetValueOrDefault(current);
		}
		catch
		{
		}
		return current;
	}

	private static ShipmentStatus ParseIntegrationStatus(string? status, ShipmentStatus fallback)
	{
		if (string.IsNullOrWhiteSpace(status))
		{
			return fallback;
		}
		string text = status.ToUpperInvariant();
		if (1 == 0)
		{
		}
		ShipmentStatus result;
		switch (text)
		{
		case "DELIVERED":
		case "DELIVERED_TO_NEIGHBOUR":
		case "DELIVERED_TO_COUNTER":
			result = ShipmentStatus.Delivered;
			break;
		case "OUT_FOR_DELIVERY":
		case "IN_TRANSIT":
		case "AT_DELIVERY_POINT":
		case "ONBOARD":
			result = ShipmentStatus.InTransit;
			break;
		case "ACCEPTED":
		case "RECEIVED_AT_WAREHOUSE":
		case "CREATED":
		case "PICKING":
			result = ShipmentStatus.ProviderAccepted;
			break;
		case "CANCELLED":
		case "RETURNED":
		case "RETURNED_TO_SENDER":
			result = ShipmentStatus.Cancelled;
			break;
		case "FAILED":
		case "DELIVERY_FAILED":
		case "UNDELIVERED":
			result = ShipmentStatus.Failed;
			break;
		default:
			result = fallback;
			break;
		}
		if (1 == 0)
		{
		}
		return result;
	}
}
