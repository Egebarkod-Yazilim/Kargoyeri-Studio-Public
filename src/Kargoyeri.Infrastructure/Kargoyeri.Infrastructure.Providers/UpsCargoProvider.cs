using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;
using Kargoyeri.Infrastructure.Providers.Blueprints;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class UpsCargoProvider : ICargoProvider
{
	private const string TokenEndpoint = "https://onlinetools.ups.com/security/v1/oauth/token";

	private const string ShipEndpoint = "https://onlinetools.ups.com/api/shipments/v2403/ship";

	private const string VoidEndpointBase = "https://onlinetools.ups.com/api/shipments/v2403/void/cancel";

	private const string TrackEndpointBase = "https://onlinetools.ups.com/api/track/v1/details";

	private readonly IEnumerable<IProviderBlueprint> _blueprints;

	private readonly IHttpClientFactory _httpClientFactory;

	private readonly ILogger<UpsCargoProvider> _logger;

	public CargoProviderType SupportedProvider => CargoProviderType.Ups;

	public UpsCargoProvider(IEnumerable<IProviderBlueprint> blueprints, IHttpClientFactory httpClientFactory, ILogger<UpsCargoProvider> logger)
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
		string clientId = credential?.Username;
		string clientSecret = credential?.ApiKey ?? GetSetting(credential, "oauthClientSecret");
		string accountNumber = credential?.ClientCode;
		if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
		{
			return ProviderShipmentResult.Fail("UPS OAuth kimlik bilgileri eksik (Username=ClientId, ApiKey=ClientSecret).", "{\"error\":\"missing_oauth_credentials\"}");
		}
		string token = await GetOAuthTokenAsync(clientId, clientSecret, cancellationToken);
		if (token == null)
		{
			return ProviderShipmentResult.Fail("UPS OAuth token alinamadi.");
		}
		LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		string invoiceNumber = LegacyProviderMappingHelpers.ResolveInvoiceNumber(shipment, "GE", 10);
		string serviceCode = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "ups.serviceCode") ?? "11";
		if (1 == 0)
		{
		}
		string text = serviceCode switch
		{
			"65" => "UPS Express Saver", 
			"07" => "UPS Express", 
			"08" => "UPS Expedited", 
			_ => "UPS Standard", 
		};
		if (1 == 0)
		{
		}
		string serviceDesc = text;
		var shipRequest = new
		{
			ShipmentRequest = new
			{
				Request = new
				{
					RequestOption = "nonvalidate"
				},
				Shipment = new
				{
					Description = "Kargo Gonderi",
					Shipper = new
					{
						Name = (GetSetting(credential, "companyName") ?? "Gonderen"),
						AttentionName = (GetSetting(credential, "companyName") ?? "Gonderen"),
						ShipperNumber = (accountNumber ?? string.Empty),
						Phone = new
						{
							Number = LegacyProviderMappingHelpers.NormalizePhone(shipment.Sender.Phone)
						},
						Address = new
						{
							AddressLine = new string[1] { GetSetting(credential, "companyAddress") ?? LegacyProviderMappingHelpers.ResolveAddress(shipment.Sender) },
							City = LegacyProviderMappingHelpers.ResolveCity(shipment.Sender),
							PostalCode = (shipment.Sender.PostalCode ?? "34000"),
							CountryCode = "TR"
						}
					},
					ShipTo = new
					{
						Name = LegacyProviderMappingHelpers.ResolveRecipientName(shipment),
						AttentionName = LegacyProviderMappingHelpers.ResolveRecipientName(shipment),
						Phone = new
						{
							Number = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone)
						},
						Address = new
						{
							AddressLine = new string[1] { LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient) },
							City = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient),
							PostalCode = (shipment.Recipient.PostalCode ?? "06000"),
							CountryCode = "TR"
						}
					},
					ShipFrom = new
					{
						Name = (GetSetting(credential, "companyName") ?? "Gonderen"),
						Address = new
						{
							AddressLine = new string[1] { GetSetting(credential, "companyAddress") ?? LegacyProviderMappingHelpers.ResolveAddress(shipment.Sender) },
							City = LegacyProviderMappingHelpers.ResolveCity(shipment.Sender),
							PostalCode = (shipment.Sender.PostalCode ?? "34000"),
							CountryCode = "TR"
						}
					},
					PaymentInformation = new
					{
						ShipmentCharge = new
						{
							Type = "01",
							BillShipper = (object)(LegacyProviderMappingHelpers.RecipientPaysShipping(shipment) ? null : new
							{
								AccountNumber = accountNumber
							}),
							BillReceiver = (object)(LegacyProviderMappingHelpers.RecipientPaysShipping(shipment) ? new
							{
								AccountNumber = accountNumber
							} : null)
						}
					},
					Service = new
					{
						Code = serviceCode,
						Description = serviceDesc
					},
					Package = shipment.Packages.Select((PackageInfo p, int idx) => new
					{
						Description = (p.Description ?? "Urun"),
						Packaging = new
						{
							Code = "02"
						},
						Dimensions = new
						{
							UnitOfMeasurement = new
							{
								Code = "CM"
							},
							Length = p.Length.GetValueOrDefault(10m).ToString("0"),
							Width = p.Width.GetValueOrDefault(10m).ToString("0"),
							Height = p.Height.GetValueOrDefault(10m).ToString("0")
						},
						PackageWeight = new
						{
							UnitOfMeasurement = new
							{
								Code = "KGS"
							},
							Weight = ((p.Weight > 0m) ? p.Weight : 1m).ToString("0.##")
						}
					}).ToArray(),
					ReferenceNumber = new
					{
						Value = invoiceNumber
					}
				},
				LabelSpecification = new
				{
					LabelImageFormat = new
					{
						Code = "PDF"
					},
					LabelStockSize = new
					{
						Height = "6",
						Width = "4"
					}
				}
			}
		};
		string endpoint = ((credential?.EndpointBase != null) ? (credential.EndpointBase.TrimEnd('/') + "/ship") : "https://onlinetools.ups.com/api/shipments/v2403/ship");
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
			HttpRequestHeaders defaultRequestHeaders = http.DefaultRequestHeaders;
			string shipmentReference = shipment.ShipmentReference;
			int length = shipmentReference.Length;
			int num = length - 8;
			defaultRequestHeaders.TryAddWithoutValidation("transId", shipmentReference.Substring(num, length - num));
			http.DefaultRequestHeaders.TryAddWithoutValidation("transactionSrc", "KargoyeriStudio");
			string json = JsonSerializer.Serialize(shipRequest);
			using StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("UPS create basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"UPS API hatasi (HTTP {response.StatusCode}).", raw);
			}
			var (trackingNumber, labelBase64, error) = ExtractShipResult(raw);
			if (error != null)
			{
				return ProviderShipmentResult.Fail("UPS hatasi: " + error, raw);
			}
			_logger.LogInformation("UPS gonderisi olusturuldu {Ref}. Takip: {Tracking}", shipment.ShipmentReference, trackingNumber);
			return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, trackingNumber, null, labelBase64, "UPS gonderisi olusturuldu.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "UPS create exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("UPS baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, "UPS simulasyon iptali.", "{\"mode\":\"simulation\"}");
		}
		string clientId = credential?.Username;
		string clientSecret = credential?.ApiKey ?? GetSetting(credential, "oauthClientSecret");
		if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
		{
			return ProviderShipmentResult.Fail("UPS OAuth kimlik bilgileri eksik.");
		}
		string token = await GetOAuthTokenAsync(clientId, clientSecret, cancellationToken);
		if (token == null)
		{
			return ProviderShipmentResult.Fail("UPS OAuth token alinamadi.");
		}
		string shipmentId = shipment.TrackingNumber ?? shipment.ShipmentReference;
		string voidUrl = "https://onlinetools.ups.com/api/shipments/v2403/void/cancel/" + Uri.EscapeDataString(shipmentId);
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, voidUrl);
			using HttpResponseMessage response = await http.SendAsync(request, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return ProviderShipmentResult.Fail($"UPS iptal API hatasi (HTTP {response.StatusCode}).", raw);
			}
			_logger.LogInformation("UPS gonderisi iptal edildi {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, string.IsNullOrWhiteSpace(reason) ? "UPS gonderisi iptal edildi." : ("UPS iptal: " + reason), raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "UPS iptal exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("UPS iptal baglanti hatasi: " + ex.Message);
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
			return ProviderShipmentResult.Ok(nextSim, shipment.TrackingNumber, null, null, $"UPS simulasyon durumu: {nextSim}.", $"{{\"mode\":\"simulation\",\"status\":\"{nextSim}\"}}");
		}
		string clientId = credential?.Username;
		string clientSecret = credential?.ApiKey ?? GetSetting(credential, "oauthClientSecret");
		if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
		{
			return ProviderShipmentResult.Fail("UPS OAuth kimlik bilgileri eksik.");
		}
		string token = await GetOAuthTokenAsync(clientId, clientSecret, cancellationToken);
		if (token == null)
		{
			return ProviderShipmentResult.Fail("UPS OAuth token alinamadi.");
		}
		string trackingNumber = shipment.TrackingNumber ?? shipment.ShipmentReference;
		string trackUrl = "https://onlinetools.ups.com/api/track/v1/details/" + Uri.EscapeDataString(trackingNumber) + "?locale=tr_TR&returnSignature=false";
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
			using HttpResponseMessage response = await http.GetAsync(trackUrl, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return ProviderShipmentResult.Fail($"UPS track API hatasi (HTTP {response.StatusCode}).", raw);
			}
			ShipmentStatus status = MapUpsStatus(raw, shipment.Status);
			_logger.LogInformation("UPS durum guncellendi {Ref}: {Status}", shipment.ShipmentReference, status);
			return ProviderShipmentResult.Ok(status, shipment.TrackingNumber, null, null, $"UPS durum: {status}.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "UPS track exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("UPS durum baglanti hatasi: " + ex.Message);
		}
	}

	private async Task<string?> GetOAuthTokenAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
	{
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(clientId + ":" + clientSecret));
			http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
			FormUrlEncodedContent formData = new FormUrlEncodedContent(new KeyValuePair<string, string>[1]
			{
				new KeyValuePair<string, string>("grant_type", "client_credentials")
			});
			using HttpResponseMessage response = await http.PostAsync("https://onlinetools.ups.com/security/v1/oauth/token", formData, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("UPS OAuth token basarisiz. HTTP {Status}: {Body}", (int)response.StatusCode, raw);
				return null;
			}
			using JsonDocument doc = JsonDocument.Parse(raw);
			if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
			{
				return tokenProp.GetString();
			}
			_logger.LogWarning("UPS OAuth token yaniti beklenmedik formatta: {Raw}", raw);
			return null;
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "UPS OAuth token istegi exception.");
			return null;
		}
	}

	private static (string? TrackingNumber, string? LabelBase64, string? Error) ExtractShipResult(string raw)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement rootElement = jsonDocument.RootElement;
			if (rootElement.TryGetProperty("response", out var value) && value.TryGetProperty("errors", out var value2) && value2.ValueKind == JsonValueKind.Array && value2.GetArrayLength() > 0)
			{
				JsonElement value3;
				string item = (value2.EnumerateArray().First().TryGetProperty("message", out value3) ? value3.GetString() : "Bilinmeyen hata");
				return (TrackingNumber: null, LabelBase64: null, Error: item);
			}
			if (!rootElement.TryGetProperty("ShipmentResponse", out var value4))
			{
				return (TrackingNumber: null, LabelBase64: null, Error: "ShipmentResponse bulunamadi.");
			}
			if (!value4.TryGetProperty("ShipmentResults", out var value5))
			{
				return (TrackingNumber: null, LabelBase64: null, Error: "ShipmentResults bulunamadi.");
			}
			string text = null;
			if (value5.TryGetProperty("PackageResults", out var value6))
			{
				foreach (JsonElement item3 in value6.EnumerateArray())
				{
					if (item3.TryGetProperty("TrackingNumber", out var value7))
					{
						text = value7.GetString();
						break;
					}
				}
			}
			if (text == null && value5.TryGetProperty("ShipmentIdentificationNumber", out var value8))
			{
				text = value8.GetString();
			}
			string item2 = null;
			if (value5.TryGetProperty("PackageResults", out var value9))
			{
				foreach (JsonElement item4 in value9.EnumerateArray())
				{
					if (item4.TryGetProperty("ShippingLabel", out var value10) && value10.TryGetProperty("GraphicImage", out var value11))
					{
						item2 = value11.GetString();
						break;
					}
				}
			}
			return (TrackingNumber: text, LabelBase64: item2, Error: null);
		}
		catch (Exception ex)
		{
			return (TrackingNumber: null, LabelBase64: null, Error: "Yanit ayristirma hatasi: " + ex.Message);
		}
	}

	private static ShipmentStatus MapUpsStatus(string raw, ShipmentStatus current)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			if (jsonDocument.RootElement.TryGetProperty("trackResponse", out var value) && value.TryGetProperty("shipment", out var value2) && value2.ValueKind == JsonValueKind.Array)
			{
				foreach (JsonElement item in value2.EnumerateArray())
				{
					if (!item.TryGetProperty("activity", out var value3) || value3.ValueKind != JsonValueKind.Array)
					{
						continue;
					}
					JsonElement jsonElement = value3.EnumerateArray().FirstOrDefault();
					if (jsonElement.ValueKind == JsonValueKind.Undefined || !jsonElement.TryGetProperty("status", out var value4))
					{
						continue;
					}
					JsonElement value5;
					string text = (value4.TryGetProperty("type", out value5) ? value5.GetString() : null);
					JsonElement value6;
					string text2 = (value4.TryGetProperty("code", out value6) ? value6.GetString() : null);
					string text3 = text?.ToUpperInvariant();
					string text4 = text2?.ToUpperInvariant();
					if (1 == 0)
					{
					}
					ShipmentStatus result;
					if (!(text3 == "D") && !(text4 == "D"))
					{
						switch (text3)
						{
						default:
							if (!(text4 == "X"))
							{
								result = current;
								break;
							}
							goto case "X";
						case "I":
						case "O":
							result = ShipmentStatus.InTransit;
							break;
						case "P":
							result = ShipmentStatus.ProviderAccepted;
							break;
						case "X":
							result = ShipmentStatus.Cancelled;
							break;
						}
					}
					else
					{
						result = ShipmentStatus.Delivered;
					}
					if (1 == 0)
					{
					}
					return result;
				}
			}
		}
		catch
		{
		}
		return current;
	}

	private static bool IsSimulation(ProviderCredential? credential)
	{
		return IsTrue(GetSetting(credential, "simulationMode"));
	}

	private ProviderShipmentResult SimulateCreate(CargoShipment shipment, ProviderCredential? credential)
	{
		ProviderRequestPreviewResponse providerRequestPreviewResponse = _blueprints.FirstOrDefault((IProviderBlueprint x) => x.SupportedProvider == CargoProviderType.Ups)?.PreviewCreateShipment(shipment, credential);
		object obj = providerRequestPreviewResponse?.TrackingNumberCandidate;
		if (obj == null)
		{
			string shipmentReference = shipment.ShipmentReference;
			int length = shipmentReference.Length;
			int num = length - 8;
			obj = "SIM-" + shipmentReference.Substring(num, length - num);
		}
		string text = (string)obj;
		_logger.LogInformation("UPS simulasyon {Ref}. Takip: {T}", shipment.ShipmentReference, text);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text, null, null, "UPS simulasyon gonderisi.", providerRequestPreviewResponse?.PayloadPreview);
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

	private static bool IsTrue(string? v)
	{
		return v != null && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("1", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase));
	}
}
