using System;
using System.Collections.Generic;
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

internal sealed class PttCargoProvider : ICargoProvider
{
	private const string DefaultEndpoint = "http://pttws.ptt.gov.tr/PttVeriYukleme/services/Sorgu.SorguHttpSoap11Endpoint/";

	private const string KabulNs = "http://kabul.ptt.gov.tr";

	private const string KabulXsd = "http://kabul.ptt.gov.tr/xsd";

	private readonly IEnumerable<IProviderBlueprint> _blueprints;

	private readonly IHttpClientFactory _httpClientFactory;

	private readonly ILogger<PttCargoProvider> _logger;

	public CargoProviderType SupportedProvider => CargoProviderType.Ptt;

	public PttCargoProvider(IEnumerable<IProviderBlueprint> blueprints, IHttpClientFactory httpClientFactory, ILogger<PttCargoProvider> logger)
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
		string endpoint = credential?.EndpointBase ?? "http://pttws.ptt.gov.tr/PttVeriYukleme/services/Sorgu.SorguHttpSoap11Endpoint/";
		string username = credential?.Username;
		string password = credential?.Password;
		string customerIdStr = GetSetting(credential, "customerId");
		string barcodeSeedStr = GetSetting(credential, "barcodeSeed");
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("PTT Kargo kimlik bilgileri eksik (Username, Password).", "{\"error\":\"missing_credentials\"}");
		}
		if (!int.TryParse(customerIdStr, out var customerId))
		{
			return ProviderShipmentResult.Fail("PTT Kargo musteri ID (customerId) yapılandırılmamış.", "{\"error\":\"missing_customerId\"}");
		}
		long.TryParse(barcodeSeedStr, out var seed);
		string nextBarcode = ((seed > 0) ? CalculateCheckDigit(seed + 1).ToString() : LegacyProviderMappingHelpers.GenerateNumericCode(12));
		string xml = BuildKabulEkle2Envelope(gonderiTip: LegacyProviderMappingHelpers.GetMetadataValue(shipment, "ptt.gonderiTip") ?? "NORMAL", username: username, password: password, customerId: customerId, adres: LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient), aliciAdi: LegacyProviderMappingHelpers.ResolveRecipientName(shipment), il: LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient), ilce: LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient), telefon: LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone), barkodNo: nextBarcode);
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using StringContent content = new StringContent(xml, Encoding.UTF8, "text/xml");
			content.Headers.Add("SOAPAction", "urn:kabulEkle2");
			using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("PTT Kargo create basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"PTT Kargo SOAP hatasi (HTTP {response.StatusCode}).", raw);
			}
			var (success, errorCode, description) = ExtractKabulResult(raw);
			if (!success)
			{
				_logger.LogWarning("PTT Kargo kabulEkle2 hatali {Ref}: {Code} - {Desc}", shipment.ShipmentReference, errorCode, description);
				return ProviderShipmentResult.Fail("PTT Kargo hatasi (" + errorCode + "): " + description, raw);
			}
			_logger.LogInformation("PTT Kargo gonderisi olusturuldu {Ref}. Barkod: {Barcode}", shipment.ShipmentReference, nextBarcode);
			return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, nextBarcode, null, null, "PTT Kargo gonderisi olusturuldu.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "PTT Kargo create exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("PTT Kargo baglanti hatasi: " + ex.Message);
		}
	}

	public Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			return Task.FromResult(ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, "PTT Kargo simulasyon iptali.", "{\"mode\":\"simulation\"}"));
		}
		_logger.LogWarning("PTT Kargo iptal operasyonu desteklenmiyor {Ref}", shipment.ShipmentReference);
		return Task.FromResult(ProviderShipmentResult.Fail("PTT Kargo API'si iptal operasyonunu desteklemiyor. Manuel iptal gerekebilir.", "{\"provider\":\"Ptt\",\"operation\":\"cancel\",\"supported\":false}"));
	}

	public Task<ProviderShipmentResult> RefreshStatusAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
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
		_logger.LogInformation("PTT Kargo durum ilerletme {Ref}: {Status}", shipment.ShipmentReference, shipmentStatus2);
		return Task.FromResult(ProviderShipmentResult.Ok(shipmentStatus2, shipment.TrackingNumber, null, null, $"PTT Kargo durum: {shipmentStatus2}.", $"{{\"provider\":\"Ptt\",\"status\":\"{shipmentStatus2}\",\"note\":\"status-via-progression\"}}"));
	}

	private static string BuildKabulEkle2Envelope(string username, string password, int customerId, string adres, string aliciAdi, string il, string ilce, string telefon, string barkodNo, string gonderiTip = "NORMAL")
	{
		return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<soapenv:Envelope\n  xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"\n  xmlns:kab=\"{"http://kabul.ptt.gov.tr"}\"\n  xmlns:xsd=\"{"http://kabul.ptt.gov.tr/xsd"}\">\n  <soapenv:Header/>\n  <soapenv:Body>\n    <kab:kabulEkle2>\n      <kab:Input2>\n        <xsd:musteriId>{customerId}</xsd:musteriId>\n        <xsd:gonderiTur>KARGO</xsd:gonderiTur>\n        <xsd:gonderiTip>{Esc(gonderiTip)}</xsd:gonderiTip>\n        <xsd:kullanici>{Esc(username)}</xsd:kullanici>\n        <xsd:sifre>{Esc(password)}</xsd:sifre>\n        <xsd:dongu>\n          <xsd:aAdres>{Esc(adres)}</xsd:aAdres>\n          <xsd:aliciAdi>{Esc(aliciAdi)}</xsd:aliciAdi>\n          <xsd:aliciIlAdi>{Esc(il)}</xsd:aliciIlAdi>\n          <xsd:aliciIlceAdi>{Esc(ilce)}</xsd:aliciIlceAdi>\n          <xsd:aliciSms>{Esc(telefon)}</xsd:aliciSms>\n          <xsd:aliciTel>{Esc(telefon)}</xsd:aliciTel>\n          <xsd:barkodNo>{Esc(barkodNo)}</xsd:barkodNo>\n          <xsd:odemesekli>N</xsd:odemesekli>\n        </xsd:dongu>\n      </kab:Input2>\n    </kab:kabulEkle2>\n  </soapenv:Body>\n</soapenv:Envelope>";
	}

	private static (bool Success, string? ErrorCode, string? Description) ExtractKabulResult(string raw)
	{
		try
		{
			XDocument xDocument = XDocument.Parse(raw);
			string text = xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "hataKodu")?.Value?.Trim();
			if (text == "0" || text == string.Empty)
			{
				return (Success: true, ErrorCode: text, Description: null);
			}
			if ((xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "donguSonuc")?.Value?.Trim())?.ToLowerInvariant() == "true")
			{
				return (Success: true, ErrorCode: "0", Description: null);
			}
			string text2 = xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "donguHataKodu")?.Value?.Trim();
			string text3 = xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "donguAciklama")?.Value?.Trim();
			return (Success: false, ErrorCode: text2 ?? text, Description: text3 ?? "Bilinmeyen hata.");
		}
		catch
		{
			return (Success: false, ErrorCode: null, Description: "SOAP yaniti anlasilamadi.");
		}
	}

	private static string CalculateCheckDigit(long number)
	{
		long num = number;
		int num2 = 0;
		for (int i = 0; i < 12; i++)
		{
			int num3 = (int)(num % 10);
			num2 += ((i % 2 == 0) ? num3 : (num3 * 3));
			num /= 10;
		}
		int value = (10 - num2 % 10) % 10;
		return $"{number}{value}";
	}

	private static bool IsSimulation(ProviderCredential? credential)
	{
		return IsTrue(GetSetting(credential, "simulationMode"));
	}

	private ProviderShipmentResult SimulateCreate(CargoShipment shipment, ProviderCredential? credential)
	{
		ProviderRequestPreviewResponse providerRequestPreviewResponse = _blueprints.FirstOrDefault((IProviderBlueprint x) => x.SupportedProvider == CargoProviderType.Ptt)?.PreviewCreateShipment(shipment, credential);
		object obj = providerRequestPreviewResponse?.TrackingNumberCandidate;
		if (obj == null)
		{
			string shipmentReference = shipment.ShipmentReference;
			int length = shipmentReference.Length;
			int num = length - 8;
			obj = "SIM-" + shipmentReference.Substring(num, length - num);
		}
		string text = (string)obj;
		_logger.LogInformation("PTT Kargo simulasyon {Ref}. Barkod: {T}", shipment.ShipmentReference, text);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text, null, null, "PTT Kargo simulasyon gonderisi.", providerRequestPreviewResponse?.PayloadPreview);
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
