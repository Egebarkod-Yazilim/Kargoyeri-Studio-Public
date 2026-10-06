using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Infrastructure.Providers.Blueprints;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class ArasCargoProvider : ICargoProvider
{
	private const string DefaultTestEndpoint = "https://customerservicestest.araskargo.com.tr/arascargoservice/arascargoservice.asmx";

	private const string SoapNs = "http://tempuri.org/";

	private readonly IEnumerable<IProviderBlueprint> _blueprints;

	private readonly IHttpClientFactory _httpClientFactory;

	private readonly ILogger<ArasCargoProvider> _logger;

	public CargoProviderType SupportedProvider => CargoProviderType.Aras;

	public ArasCargoProvider(IEnumerable<IProviderBlueprint> blueprints, IHttpClientFactory httpClientFactory, ILogger<ArasCargoProvider> logger)
	{
		_blueprints = blueprints;
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	public async Task<ProviderShipmentResult> CreateShipmentAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		CargoShipment shipment2 = shipment;
		if (IsSimulation(credential))
		{
			return SimulateCreate(shipment2, credential);
		}
		string endpoint = credential?.EndpointBase ?? "https://customerservicestest.araskargo.com.tr/arascargoservice/arascargoservice.asmx";
		string username = credential?.Username;
		string password = credential?.Password;
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("Aras Kargo kimlik bilgileri eksik (Username, Password).", "{\"error\":\"missing_credentials\"}");
		}
		LegacyPaymentKind paymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment2);
		bool isCod = paymentKind != LegacyPaymentKind.Prepaid;
		string integrationCode = LegacyProviderMappingHelpers.GenerateNumericCode(15);
		string tradingWaybill = LegacyProviderMappingHelpers.ResolveInvoiceNumber(shipment2, "GE", 14);
		int packageCount = LegacyProviderMappingHelpers.GetPackageCount(shipment2);
		var pieces = (from i in Enumerable.Range(1, packageCount)
			select new
			{
				BarcodeNumber = $"{integrationCode}{i}",
				VolumetricWeight = LegacyProviderMappingHelpers.SumDesi(shipment2).ToString("0.##", CultureInfo.InvariantCulture),
				Weight = LegacyProviderMappingHelpers.SumWeight(shipment2).ToString("0.##", CultureInfo.InvariantCulture)
			}).ToList();
		string username2 = username;
		string password2 = password;
		string tradingWaybill2 = tradingWaybill;
		string integrationCode2 = integrationCode;
		string receiverName = LegacyProviderMappingHelpers.ResolveRecipientName(shipment2);
		string receiverAddress = LegacyProviderMappingHelpers.ResolveAddress(shipment2.Recipient);
		string receiverPhone = LegacyProviderMappingHelpers.NormalizePhone(shipment2.Recipient.Phone);
		string receiverCity = LegacyProviderMappingHelpers.ResolveCity(shipment2.Recipient);
		string receiverTown = LegacyProviderMappingHelpers.ResolveDistrict(shipment2.Recipient);
		string volWeight = LegacyProviderMappingHelpers.SumDesi(shipment2).ToString("0.##", CultureInfo.InvariantCulture);
		string weight = LegacyProviderMappingHelpers.SumWeight(shipment2).ToString("0.##", CultureInfo.InvariantCulture);
		string pieceCount = packageCount.ToString();
		string payorTypeCode = (LegacyProviderMappingHelpers.RecipientPaysShipping(shipment2) ? "2" : "1");
		string isCod2 = (isCod ? "1" : "0");
		if (1 == 0)
		{
		}
		string codCollectionType = paymentKind switch
		{
			LegacyPaymentKind.CashOnDeliveryCash => "0", 
			LegacyPaymentKind.CashOnDeliveryCard => "1", 
			_ => "0", 
		};
		if (1 == 0)
		{
		}
		string xml = BuildSetOrderEnvelope(username2, password2, tradingWaybill2, integrationCode2, receiverName, receiverAddress, receiverPhone, receiverCity, receiverTown, volWeight, weight, pieceCount, payorTypeCode, isCod2, codCollectionType, isCod ? LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment2).ToString("0.##", CultureInfo.InvariantCulture) : "0", pieces.Select(p => (BarcodeNumber: p.BarcodeNumber, VolumetricWeight: p.VolumetricWeight, Weight: p.Weight)).ToList());
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using StringContent content = new StringContent(xml, Encoding.UTF8, "text/xml");
			content.Headers.Add("SOAPAction", "\"http://tempuri.org/SetOrder\"");
			using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Aras Kargo create basarisiz {Ref}. HTTP {Status}: {Body}", shipment2.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"Aras Kargo SOAP hatasi (HTTP {response.StatusCode}).", raw);
			}
			var (resultCode, resultMessage) = ExtractSetOrderResult(raw);
			if (resultCode != "0" && resultCode != null)
			{
				_logger.LogWarning("Aras Kargo SetOrder hatali {Ref}: {Code} - {Msg}", shipment2.ShipmentReference, resultCode, resultMessage);
				return ProviderShipmentResult.Fail("Aras Kargo hatasi (" + resultCode + "): " + resultMessage, raw);
			}
			_logger.LogInformation("Aras Kargo gonderisi olusturuldu {Ref}. IntegrationCode: {Code}", shipment2.ShipmentReference, integrationCode);
			return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, integrationCode, null, null, "Aras Kargo gonderisi olusturuldu.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "Aras Kargo create exception {Ref}", shipment2.ShipmentReference);
			return ProviderShipmentResult.Fail("Aras Kargo baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, "Aras Kargo simulasyon iptali.", "{\"mode\":\"simulation\"}");
		}
		string endpoint = credential?.EndpointBase ?? "https://customerservicestest.araskargo.com.tr/arascargoservice/arascargoservice.asmx";
		string username = credential?.Username;
		string password = credential?.Password;
		string integrationCode = shipment.TrackingNumber ?? shipment.ShipmentReference;
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("Aras Kargo kimlik bilgileri eksik.");
		}
		string xml = BuildCancelEnvelope(username, password, integrationCode);
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using StringContent content = new StringContent(xml, Encoding.UTF8, "text/xml");
			content.Headers.Add("SOAPAction", "\"http://tempuri.org/CancelDispatch\"");
			using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return ProviderShipmentResult.Fail($"Aras Kargo iptal SOAP hatasi (HTTP {response.StatusCode}).", raw);
			}
			_logger.LogInformation("Aras Kargo gonderisi iptal edildi {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, string.IsNullOrWhiteSpace(reason) ? "Aras Kargo gonderisi iptal edildi." : ("Aras Kargo iptal: " + reason), raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "Aras Kargo iptal exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Aras Kargo iptal baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> RefreshStatusAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential) || string.IsNullOrWhiteSpace(shipment.TrackingNumber))
		{
			return SimulateRefresh(shipment);
		}
		string endpoint = credential?.EndpointBase ?? "https://customerservicestest.araskargo.com.tr/arascargoservice/arascargoservice.asmx";
		string username = credential?.Username;
		string password = credential?.Password;
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			return SimulateRefresh(shipment);
		}
		string xml = BuildGetOrderEnvelope(username, password, shipment.TrackingNumber);
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using StringContent content = new StringContent(xml, Encoding.UTF8, "text/xml");
			content.Headers.Add("SOAPAction", "\"http://tempuri.org/GetOrderByBarcodeNumber\"");
			using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Aras Kargo durum sorgu hatasi {Ref}. HTTP {Status}", shipment.ShipmentReference, (int)response.StatusCode);
				return SimulateRefresh(shipment);
			}
			(string, string) tuple = ExtractOrderStatus(raw);
			string statusCode = tuple.Item1;
			string statusDesc = tuple.Item2;
			ShipmentStatus mapped = MapArasStatus(statusCode, shipment.Status);
			_logger.LogInformation("Aras Kargo durum guncellendi {Ref}: {Code} -> {Status}", shipment.ShipmentReference, statusCode, mapped);
			return ProviderShipmentResult.Ok(mapped, shipment.TrackingNumber, null, null, "Aras Kargo durum: " + (statusDesc ?? mapped.ToString()) + ".", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogWarning(ex, "Aras Kargo durum sorgu exception {Ref}. Simulasyona donuluyor.", shipment.ShipmentReference);
			return SimulateRefresh(shipment);
		}
	}

	private static ShipmentStatus MapArasStatus(string? code, ShipmentStatus current)
	{
		if (1 == 0)
		{
		}
		ShipmentStatus result = code switch
		{
			"1" => ShipmentStatus.Delivered, 
			"10" => ShipmentStatus.Delivered, 
			"2" => ShipmentStatus.InTransit, 
			"3" => ShipmentStatus.InTransit, 
			"4" => ShipmentStatus.ProviderAccepted, 
			"5" => ShipmentStatus.Cancelled, 
			"6" => ShipmentStatus.Failed, 
			_ => current, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private ProviderShipmentResult SimulateRefresh(CargoShipment shipment)
	{
		ShipmentStatus status = shipment.Status;
		if (1 == 0)
		{
		}
		ShipmentStatus shipmentStatus;
		switch (status)
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
		ShipmentStatus shipmentStatus2 = shipmentStatus;
		_logger.LogInformation("Aras Kargo durum ilerletme {Ref}: {Status}", shipment.ShipmentReference, shipmentStatus2);
		return ProviderShipmentResult.Ok(shipmentStatus2, shipment.TrackingNumber, null, null, $"Aras Kargo durum: {shipmentStatus2}.", $"{{\"provider\":\"Aras\",\"status\":\"{shipmentStatus2}\",\"note\":\"simulation\"}}");
	}

	private static string BuildSetOrderEnvelope(string username, string password, string tradingWaybill, string integrationCode, string receiverName, string receiverAddress, string receiverPhone, string receiverCity, string receiverTown, string volWeight, string weight, string pieceCount, string payorTypeCode, string isCod, string codCollectionType, string codAmount, List<(string Barcode, string VolWeight, string Weight)> pieces)
	{
		string value = string.Concat(pieces.Select<(string, string, string), string>(((string Barcode, string VolWeight, string Weight) p) => $"<PieceDetail>\n  <BarcodeNumber>{Esc(p.Barcode)}</BarcodeNumber>\n  <VolumetricWeight>{Esc(p.VolWeight)}</VolumetricWeight>\n  <Weight>{Esc(p.Weight)}</Weight>\n</PieceDetail>"));
		return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n               xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"\n               xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\n  <soap:Body>\n    <SetOrder xmlns=\"{"http://tempuri.org/"}\">\n      <orderInfo>\n        <Order>\n          <UserName>{Esc(username)}</UserName>\n          <Password>{Esc(password)}</Password>\n          <TradingWaybillNumber>{Esc(tradingWaybill)}</TradingWaybillNumber>\n          <IntegrationCode>{Esc(integrationCode)}</IntegrationCode>\n          <ReceiverName>{Esc(receiverName)}</ReceiverName>\n          <ReceiverAddress>{Esc(receiverAddress)}</ReceiverAddress>\n          <ReceiverPhone1>{Esc(receiverPhone)}</ReceiverPhone1>\n          <ReceiverPhone2></ReceiverPhone2>\n          <ReceiverPhone3></ReceiverPhone3>\n          <ReceiverCityName>{Esc(receiverCity)}</ReceiverCityName>\n          <ReceiverTownName>{Esc(receiverTown)}</ReceiverTownName>\n          <VolumetricWeight>{Esc(volWeight)}</VolumetricWeight>\n          <Weight>{Esc(weight)}</Weight>\n          <PieceCount>{Esc(pieceCount)}</PieceCount>\n          <PayorTypeCode>{Esc(payorTypeCode)}</PayorTypeCode>\n          <IsWorldWide>0</IsWorldWide>\n          <IsCod>{Esc(isCod)}</IsCod>\n          <CodAmount>{Esc(codAmount)}</CodAmount>\n          <CodCollectionType>{Esc(codCollectionType)}</CodCollectionType>\n          <CodBillingType>0</CodBillingType>\n          <Description></Description>\n          <Country>Turkiye</Country>\n          <CountryCode>TR</CountryCode>\n          <CityCode></CityCode>\n          <TownCode></TownCode>\n          <PieceDetails>{value}</PieceDetails>\n        </Order>\n      </orderInfo>\n      <userName>{Esc(username)}</userName>\n      <password>{Esc(password)}</password>\n    </SetOrder>\n  </soap:Body>\n</soap:Envelope>";
	}

	private static string BuildGetOrderEnvelope(string username, string password, string barcodeNumber)
	{
		return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n               xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"\n               xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\n  <soap:Body>\n    <GetOrderByBarcodeNumber xmlns=\"{"http://tempuri.org/"}\">\n      <userName>{Esc(username)}</userName>\n      <password>{Esc(password)}</password>\n      <barcodeNumber>{Esc(barcodeNumber)}</barcodeNumber>\n    </GetOrderByBarcodeNumber>\n  </soap:Body>\n</soap:Envelope>";
	}

	private static string BuildCancelEnvelope(string username, string password, string integrationCode)
	{
		return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n               xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"\n               xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\n  <soap:Body>\n    <CancelDispatch xmlns=\"{"http://tempuri.org/"}\">\n      <userName>{Esc(username)}</userName>\n      <password>{Esc(password)}</password>\n      <integrationCode>{Esc(integrationCode)}</integrationCode>\n    </CancelDispatch>\n  </soap:Body>\n</soap:Envelope>";
	}

	private static (string? ResultCode, string? ResultMessage) ExtractSetOrderResult(string raw)
	{
		try
		{
			XDocument xDocument = XDocument.Parse(raw);
			string item = xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "ResultCode")?.Value?.Trim();
			string item2 = xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "ResultMessage")?.Value?.Trim();
			return (ResultCode: item, ResultMessage: item2);
		}
		catch
		{
			return (ResultCode: null, ResultMessage: null);
		}
	}

	private static (string? StatusCode, string? StatusDesc) ExtractOrderStatus(string raw)
	{
		try
		{
			XDocument xDocument = XDocument.Parse(raw);
			string item = xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "StatusCode")?.Value?.Trim();
			string item2 = xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "StatusDescription")?.Value?.Trim();
			return (StatusCode: item, StatusDesc: item2);
		}
		catch
		{
			return (StatusCode: null, StatusDesc: null);
		}
	}

	private static bool IsSimulation(ProviderCredential? credential)
	{
		return IsTrue(GetSetting(credential, "simulationMode"));
	}

	private ProviderShipmentResult SimulateCreate(CargoShipment shipment, ProviderCredential? credential)
	{
		ProviderRequestPreviewResponse providerRequestPreviewResponse = _blueprints.FirstOrDefault((IProviderBlueprint x) => x.SupportedProvider == CargoProviderType.Aras)?.PreviewCreateShipment(shipment, credential);
		object obj = providerRequestPreviewResponse?.TrackingNumberCandidate;
		if (obj == null)
		{
			string shipmentReference = shipment.ShipmentReference;
			int length = shipmentReference.Length;
			int num = length - 8;
			obj = "SIM-" + shipmentReference.Substring(num, length - num);
		}
		string text = (string)obj;
		_logger.LogInformation("Aras Kargo simulasyon {Ref}. Takip: {T}", shipment.ShipmentReference, text);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text, null, null, "Aras Kargo simulasyon gonderisi.", providerRequestPreviewResponse?.PayloadPreview);
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

	private static string Esc(string? v)
	{
		return string.IsNullOrEmpty(v) ? string.Empty : v.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
			.Replace("\"", "&quot;");
	}
}
