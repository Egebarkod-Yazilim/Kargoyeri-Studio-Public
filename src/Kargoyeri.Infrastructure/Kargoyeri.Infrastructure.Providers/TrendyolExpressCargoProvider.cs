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
using Kargoyeri.Infrastructure.Providers.Blueprints;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class TrendyolExpressCargoProvider : ICargoProvider
{
	private const string DefaultBaseUrl = "https://api.trendyol.com/sapigw/suppliers";

	private readonly IEnumerable<IProviderBlueprint> _blueprints;

	private readonly IHttpClientFactory _httpClientFactory;

	private readonly ILogger<TrendyolExpressCargoProvider> _logger;

	public CargoProviderType SupportedProvider => CargoProviderType.TrendyolExpress;

	public TrendyolExpressCargoProvider(IEnumerable<IProviderBlueprint> blueprints, IHttpClientFactory httpClientFactory, ILogger<TrendyolExpressCargoProvider> logger)
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
		string sellerId = credential?.ClientCode;
		string username = credential?.Username;
		string password = credential?.Password;
		if (string.IsNullOrWhiteSpace(sellerId) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("Trendyol Express kimlik bilgileri eksik (Username, Password, ClientCode=SellerId).", "{\"error\":\"missing_credentials\"}");
		}
		string packageId = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "trendyol.packageId", "packageId") ?? shipment.OrderReference;
		string warehouseId = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "trendyol.warehouseId", "warehouseId") ?? GetSetting(credential, "warehouseId");
		if (string.IsNullOrWhiteSpace(warehouseId))
		{
			return ProviderShipmentResult.Fail("Trendyol Express depo ID (warehouseId) eksik. Metadata veya ayarlarda belirtin.", "{\"error\":\"missing_warehouseId\"}");
		}
		string baseUrl = ResolveBaseUrl(credential, sellerId);
		string url = baseUrl + "/orders/" + Uri.EscapeDataString(packageId) + "/update-warehouse-information";
		int wid;
		string body = JsonSerializer.Serialize(new
		{
			warehouseId = (int.TryParse(warehouseId, out wid) ? ((object)wid) : warehouseId)
		});
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			SetBasicAuth(http, username, password);
			using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
			using HttpResponseMessage response = await http.PutAsync(url, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Trendyol Express updateWarehouse basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"Trendyol Express API hatasi (HTTP {response.StatusCode}).", raw);
			}
			_logger.LogInformation("Trendyol Express depo guncellendi {Ref}. PackageId: {PkgId}, WarehouseId: {WId}", shipment.ShipmentReference, packageId, warehouseId);
			return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, packageId, null, null, "Trendyol Express depo guncellendi.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "Trendyol Express updateWarehouse exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Trendyol Express baglanti hatasi: " + ex.Message);
		}
	}

	public Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			return Task.FromResult(ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, "Trendyol Express simulasyon iptali.", "{\"mode\":\"simulation\"}"));
		}
		_logger.LogWarning("Trendyol Express iptal API destegi yok {Ref}", shipment.ShipmentReference);
		return Task.FromResult(ProviderShipmentResult.Fail("Trendyol Express paket iptali Trendyol Seller Panel üzerinden yapılmalıdır.", "{\"provider\":\"TrendyolExpress\",\"operation\":\"cancel\",\"supported\":false}"));
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
			return ProviderShipmentResult.Ok(nextSim, shipment.TrackingNumber, null, null, $"Trendyol Express simulasyon durumu: {nextSim}.", $"{{\"mode\":\"simulation\",\"status\":\"{nextSim}\"}}");
		}
		string sellerId = credential?.ClientCode;
		string username = credential?.Username;
		string password = credential?.Password;
		if (string.IsNullOrWhiteSpace(sellerId) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("Trendyol Express kimlik bilgileri eksik.");
		}
		string packageId = shipment.TrackingNumber ?? LegacyProviderMappingHelpers.GetMetadataValue(shipment, "trendyol.packageId", "packageId") ?? shipment.OrderReference;
		string baseUrl = ResolveBaseUrl(credential, sellerId);
		string url = baseUrl + "/orders/shipment-packages?orderNumber=" + Uri.EscapeDataString(packageId);
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			SetBasicAuth(http, username, password);
			using HttpResponseMessage response = await http.GetAsync(url, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return ProviderShipmentResult.Fail($"Trendyol Express durum sorgu hatasi (HTTP {response.StatusCode}).", raw);
			}
			var (status, cargoTrackingNumber) = MapTrendyolStatus(raw, shipment.Status);
			_logger.LogInformation("Trendyol Express durum guncellendi {Ref}: {Status}", shipment.ShipmentReference, status);
			return ProviderShipmentResult.Ok(status, cargoTrackingNumber ?? shipment.TrackingNumber, null, null, $"Trendyol Express durum: {status}.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "Trendyol Express refresh exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Trendyol Express durum baglanti hatasi: " + ex.Message);
		}
	}

	private static string ResolveBaseUrl(ProviderCredential? credential, string sellerId)
	{
		string text = credential?.EndpointBase;
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text.TrimEnd('/');
		}
		return "https://api.trendyol.com/sapigw/suppliers/" + Uri.EscapeDataString(sellerId);
	}

	private static void SetBasicAuth(HttpClient http, string username, string password)
	{
		string parameter = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", parameter);
	}

	private static (ShipmentStatus Status, string? CargoTrackingNumber) MapTrendyolStatus(string raw, ShipmentStatus current)
	{
		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement rootElement = jsonDocument.RootElement;
			JsonElement? jsonElement = null;
			if (rootElement.TryGetProperty("content", out var value) && value.ValueKind == JsonValueKind.Array)
			{
				jsonElement = value.EnumerateArray().FirstOrDefault();
			}
			else if (rootElement.ValueKind == JsonValueKind.Array)
			{
				jsonElement = rootElement.EnumerateArray().FirstOrDefault();
			}
			if (!jsonElement.HasValue || jsonElement.Value.ValueKind == JsonValueKind.Undefined)
			{
				return (Status: current, CargoTrackingNumber: null);
			}
			JsonElement value2 = jsonElement.Value;
			string item = null;
			if (value2.TryGetProperty("cargoTrackingNumber", out var value3))
			{
				item = value3.GetString();
			}
			if (!value2.TryGetProperty("status", out var value4))
			{
				return (Status: current, CargoTrackingNumber: item);
			}
			string text = value4.GetString()?.ToUpperInvariant();
			if (1 == 0)
			{
			}
			ShipmentStatus shipmentStatus;
			switch (text)
			{
			case "Delivered":
			case "DELIVERED":
				shipmentStatus = ShipmentStatus.Delivered;
				break;
			case "Shipped":
			case "SHIPPED":
			case "InShipping":
			case "INSHIPPING":
				shipmentStatus = ShipmentStatus.InTransit;
				break;
			case "Invoiced":
			case "INVOICED":
			case "Picking":
			case "PICKING":
			case "Created":
			case "CREATED":
				shipmentStatus = ShipmentStatus.ProviderAccepted;
				break;
			case "Cancelled":
			case "CANCELLED":
			case "UnDelivered":
			case "UNDELIVERED":
				shipmentStatus = ShipmentStatus.Cancelled;
				break;
			default:
				shipmentStatus = current;
				break;
			}
			if (1 == 0)
			{
			}
			ShipmentStatus item2 = shipmentStatus;
			return (Status: item2, CargoTrackingNumber: item);
		}
		catch
		{
		}
		return (Status: current, CargoTrackingNumber: null);
	}

	private static bool IsSimulation(ProviderCredential? credential)
	{
		return IsTrue(GetSetting(credential, "simulationMode"));
	}

	private ProviderShipmentResult SimulateCreate(CargoShipment shipment, ProviderCredential? credential)
	{
		ProviderRequestPreviewResponse providerRequestPreviewResponse = _blueprints.FirstOrDefault((IProviderBlueprint x) => x.SupportedProvider == CargoProviderType.TrendyolExpress)?.PreviewCreateShipment(shipment, credential);
		string text = providerRequestPreviewResponse?.TrackingNumberCandidate ?? shipment.OrderReference;
		_logger.LogInformation("Trendyol Express simulasyon {Ref}. PackageId: {T}", shipment.ShipmentReference, text);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text, null, null, "Trendyol Express simulasyon gonderisi.", providerRequestPreviewResponse?.PayloadPreview);
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
