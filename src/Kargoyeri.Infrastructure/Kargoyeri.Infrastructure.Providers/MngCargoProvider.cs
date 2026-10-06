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

internal sealed class MngCargoProvider : ICargoProvider
{
	private const string DefaultSoapEndpoint = "http://service.mngkargo.com.tr/musterikargosiparis/musterikargosiparis.asmx";

	private const string SoapNs = "http://tempuri.org/";

	private readonly IEnumerable<IProviderBlueprint> _blueprints;

	private readonly IHttpClientFactory _httpClientFactory;

	private readonly ILogger<MngCargoProvider> _logger;

	public CargoProviderType SupportedProvider => CargoProviderType.Mng;

	public MngCargoProvider(IEnumerable<IProviderBlueprint> blueprints, IHttpClientFactory httpClientFactory, ILogger<MngCargoProvider> logger)
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
		string endpoint = credential?.EndpointBase ?? "http://service.mngkargo.com.tr/musterikargosiparis/musterikargosiparis.asmx";
		string username = credential?.Username;
		string password = credential?.Password;
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("MNG Kargo kimlik bilgileri eksik (Username, Password).", "{\"error\":\"missing_credentials\"}");
		}
		LegacyPaymentKind paymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		string invoiceNo = LegacyProviderMappingHelpers.ResolveInvoiceNumber(shipment, "GE", 8);
		string barcode = LegacyProviderMappingHelpers.GenerateNumericCode(10);
		string paketIcerik = LegacyProviderMappingHelpers.BuildPackageContent(shipment);
		string payorType = (LegacyProviderMappingHelpers.RecipientPaysShipping(shipment) ? "U" : "P");
		int isCod = ((paymentKind != 0) ? 1 : 0);
		string amount = LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
		string xml = BuildSiparisEnvelope(invoiceNo, amount, barcode, paketIcerik, LegacyProviderMappingHelpers.ResolveRecipientName(shipment), payorType, LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient), LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient), LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient), LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone), shipment.Recipient.Email ?? string.Empty, isCod, username, password);
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using StringContent content = new StringContent(xml, Encoding.UTF8, "text/xml");
			content.Headers.Add("SOAPAction", "\"http://tempuri.org/SiparisGirisiDetayliV2\"");
			using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("MNG Kargo create basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"MNG Kargo SOAP hatasi (HTTP {response.StatusCode}).", raw);
			}
			string result = ExtractSiparisResult(raw);
			if (result != "1" && result != null)
			{
				_logger.LogWarning("MNG Kargo create hatali sonuc {Ref}: {Result}", shipment.ShipmentReference, result);
				return ProviderShipmentResult.Fail("MNG Kargo hatasi: " + result, raw);
			}
			_logger.LogInformation("MNG Kargo gonderisi olusturuldu {Ref}. Barkod: {Barcode}", shipment.ShipmentReference, barcode);
			return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, barcode, null, null, "MNG Kargo gonderisi olusturuldu.", raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "MNG Kargo create exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("MNG Kargo baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, "MNG Kargo simulasyon iptali.", "{\"mode\":\"simulation\"}");
		}
		string endpoint = credential?.EndpointBase ?? "http://service.mngkargo.com.tr/musterikargosiparis/musterikargosiparis.asmx";
		string username = credential?.Username;
		string password = credential?.Password;
		string barcode = shipment.TrackingNumber ?? shipment.ShipmentReference;
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			return ProviderShipmentResult.Fail("MNG Kargo kimlik bilgileri eksik.");
		}
		string xml = BuildIptalEnvelope(username, password, barcode);
		try
		{
			using HttpClient http = _httpClientFactory.CreateClient();
			using StringContent content = new StringContent(xml, Encoding.UTF8, "text/xml");
			content.Headers.Add("SOAPAction", "\"http://tempuri.org/MusteriSiparisIptal\"");
			using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return ProviderShipmentResult.Fail($"MNG Kargo iptal SOAP hatasi (HTTP {response.StatusCode}).", raw);
			}
			string result = ExtractIptalResult(raw);
			if (result != "1" && result != null)
			{
				return ProviderShipmentResult.Fail("MNG Kargo iptal hatasi: " + result, raw);
			}
			_logger.LogInformation("MNG Kargo gonderisi iptal edildi {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(ShipmentStatus.Cancelled, shipment.TrackingNumber, null, null, string.IsNullOrWhiteSpace(reason) ? "MNG Kargo gonderisi iptal edildi." : ("MNG iptal: " + reason), raw);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			_logger.LogError(ex, "MNG Kargo iptal exception {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("MNG Kargo iptal baglanti hatasi: " + ex.Message);
		}
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
		_logger.LogInformation("MNG Kargo durum guncelleme {Ref}: {Status} (simulated)", shipment.ShipmentReference, shipmentStatus2);
		return Task.FromResult(ProviderShipmentResult.Ok(shipmentStatus2, shipment.TrackingNumber, null, null, $"MNG Kargo durum: {shipmentStatus2}.", $"{{\"provider\":\"Mng\",\"status\":\"{shipmentStatus2}\",\"note\":\"status-via-simulation\"}}"));
	}

	private static string BuildSiparisEnvelope(string irsaliyeNo, string kiymet, string barcode, string paketIcerik, string aliciAdi, string odemeSekli, string il, string ilce, string adres, string telCep, string email, int kapidaOdeme, string username, string password)
	{
		return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n               xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"\n               xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\n  <soap:Body>\n    <SiparisGirisiDetayliV2 xmlns=\"{"http://tempuri.org/"}\">\n      <pChIrsaliyeNo>{Esc(irsaliyeNo)}</pChIrsaliyeNo>\n      <pPrKiymet>{Esc(kiymet)}</pPrKiymet>\n      <pChBarkod>{Esc(barcode)}</pChBarkod>\n      <pChIcerik>Urun</pChIcerik>\n      <pFlAlSms>0</pFlAlSms>\n      <pFlGnSms>0</pFlGnSms>\n      <pKargoParcaList>{Esc(paketIcerik)}</pKargoParcaList>\n      <pAliciMusteriMngNo></pAliciMusteriMngNo>\n      <pAliciMusteriBayiNo></pAliciMusteriBayiNo>\n      <pAliciMusteriAdi>{Esc(aliciAdi)}</pAliciMusteriAdi>\n      <pChSiparisNo>{Esc(barcode)}</pChSiparisNo>\n      <pLuOdemeSekli>{Esc(odemeSekli)}</pLuOdemeSekli>\n      <pFlAdresFarkli>0</pFlAdresFarkli>\n      <pChIl>{Esc(il)}</pChIl>\n      <pChIlce>{Esc(ilce)}</pChIlce>\n      <pChAdres>{Esc(adres)}</pChAdres>\n      <pChSemt></pChSemt>\n      <pChMahalle></pChMahalle>\n      <pChMeydanBulvar></pChMeydanBulvar>\n      <pChCadde></pChCadde>\n      <pChSokak></pChSokak>\n      <pChTelEv></pChTelEv>\n      <pChTelCep>{Esc(telCep)}</pChTelCep>\n      <pChTelIs></pChTelIs>\n      <pChFax></pChFax>\n      <pChEmail>{Esc(email)}</pChEmail>\n      <pChVergiDairesi></pChVergiDairesi>\n      <pChVergiNumarasi></pChVergiNumarasi>\n      <pFlKapidaOdeme>{kapidaOdeme}</pFlKapidaOdeme>\n      <pKullaniciAdi>{Esc(username)}</pKullaniciAdi>\n      <pSifre>{Esc(password)}</pSifre>\n    </SiparisGirisiDetayliV2>\n  </soap:Body>\n</soap:Envelope>";
	}

	private static string BuildIptalEnvelope(string username, string password, string siparisNo)
	{
		return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n               xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"\n               xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\n  <soap:Body>\n    <MusteriSiparisIptal xmlns=\"{"http://tempuri.org/"}\">\n      <kullaniciAdi>{Esc(username)}</kullaniciAdi>\n      <sifre>{Esc(password)}</sifre>\n      <siparisNo>{Esc(siparisNo)}</siparisNo>\n      <siparisDate></siparisDate>\n    </MusteriSiparisIptal>\n  </soap:Body>\n</soap:Envelope>";
	}

	private static string? ExtractSiparisResult(string raw)
	{
		try
		{
			XDocument xDocument = XDocument.Parse(raw);
			return xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "SiparisGirisiDetayliV2Result")?.Value?.Trim();
		}
		catch
		{
			return null;
		}
	}

	private static string? ExtractIptalResult(string raw)
	{
		try
		{
			XDocument xDocument = XDocument.Parse(raw);
			return xDocument.Descendants().FirstOrDefault((XElement e) => e.Name.LocalName == "MusteriSiparisIptalResult")?.Value?.Trim();
		}
		catch
		{
			return null;
		}
	}

	private static bool IsSimulation(ProviderCredential? credential)
	{
		return IsTrue(GetSetting(credential, "simulationMode"));
	}

	private ProviderShipmentResult SimulateCreate(CargoShipment shipment, ProviderCredential? credential)
	{
		ProviderRequestPreviewResponse providerRequestPreviewResponse = _blueprints.FirstOrDefault((IProviderBlueprint x) => x.SupportedProvider == CargoProviderType.Mng)?.PreviewCreateShipment(shipment, credential);
		object obj = providerRequestPreviewResponse?.TrackingNumberCandidate;
		if (obj == null)
		{
			string shipmentReference = shipment.ShipmentReference;
			int length = shipmentReference.Length;
			int num = length - 8;
			obj = "SIM-" + shipmentReference.Substring(num, length - num);
		}
		string text = (string)obj;
		_logger.LogInformation("MNG Kargo simulasyon {Ref}. Takip: {T}", shipment.ShipmentReference, text);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, text, null, null, "MNG Kargo simulasyon gonderisi.", providerRequestPreviewResponse?.PayloadPreview);
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
