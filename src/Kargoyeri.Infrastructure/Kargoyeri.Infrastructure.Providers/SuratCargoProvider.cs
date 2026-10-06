using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

internal sealed class SuratCargoProvider : ICargoProvider
{
	internal const string DefaultRestBaseUrl = "https://api02.suratkargo.com.tr";
	internal const string LiveRestBaseUrl = "https://api01.suratkargo.com.tr";
	internal const string DefaultSoapEndpoint = "https://webservices.suratkargo.com.tr/services.asmx";
	internal const string TestSoapEndpoint = "https://prova.suratkargo.com.tr/services.asmx";
	private const string SoapNs = "http://tempuri.org/";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

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

		if (!TryResolveCredentials(shipment, credential, out string cariKodu, out string sifre, out string? missing))
		{
			return ProviderShipmentResult.Fail(missing!, "{\"error\":\"missing_credentials\"}");
		}

		SuratGonderiPayload payload = BuildGonderi(shipment, credential);
		if (payload.ValidationError is not null)
		{
			return ProviderShipmentResult.Fail(payload.ValidationError);
		}

		try
		{
			return UsesSoap(credential)
				? await CreateViaSoapAsync(shipment, credential, cariKodu, sifre, payload, cancellationToken).ConfigureAwait(false)
				: await CreateViaRestAsync(shipment, credential, cariKodu, sifre, payload, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Sürat Kargo gönderi isteği hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Surat Kargo baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			_logger.LogInformation("Surat Kargo simulasyon iptali {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(
				ShipmentStatus.Cancelled,
				shipment.TrackingNumber,
				shipment.LabelUrl,
				shipment.LabelContentBase64,
				string.IsNullOrWhiteSpace(reason) ? "Surat Kargo simulasyon iptali." : ("Surat Kargo simulasyon iptali: " + reason),
				"{\"mode\":\"simulation\",\"operation\":\"cancel\"}");
		}

		if (!TryResolveCredentials(shipment, credential, out string cariKodu, out string sifre, out string? missing))
		{
			return ProviderShipmentResult.Fail(missing!, "{\"error\":\"missing_credentials\"}");
		}

		string webOrderCode = ResolveWebSiparisKodu(shipment);
		string url = $"{ResolveRestBaseUrl(credential)}/api/Gonderi/GonderiSil?CariKodu={Uri.EscapeDataString(cariKodu)}&Sifre={Uri.EscapeDataString(sifre)}&WebSiparisKodu={Uri.EscapeDataString(webOrderCode)}";
		try
		{
			using HttpClient http = CreateClient();
			using HttpResponseMessage response = await http.PostAsync(url, null, cancellationToken).ConfigureAwait(false);
			string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				_logger.LogWarning("Surat Kargo iptal basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
				return ProviderShipmentResult.Fail($"Surat Kargo iptal API hatasi (HTTP {response.StatusCode}).", raw);
			}

			string? error = ExtractErrorMessage(raw);
			if (error is not null)
			{
				return ProviderShipmentResult.Fail("Surat Kargo iptal hatasi: " + error, raw);
			}

			_logger.LogInformation("Surat Kargo gonderisi iptal edildi {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Ok(
				ShipmentStatus.Cancelled,
				shipment.TrackingNumber,
				null,
				null,
				string.IsNullOrWhiteSpace(reason) ? "Surat Kargo gonderisi iptal edildi." : ("Surat Kargo iptal: " + reason),
				raw);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Surat Kargo iptal istegi hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Surat Kargo iptal baglanti hatasi: " + ex.Message);
		}
	}

	public async Task<ProviderShipmentResult> RefreshStatusAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken)
	{
		if (IsSimulation(credential))
		{
			ShipmentStatus nextSim = SimulateNextStatus(shipment);
			_logger.LogInformation("Surat Kargo simulasyon durum guncelleme {Ref}: {Status}", shipment.ShipmentReference, nextSim);
			return ProviderShipmentResult.Ok(nextSim, shipment.TrackingNumber, null, null, $"Surat Kargo simulasyon durumu: {nextSim}.", $"{{\"mode\":\"simulation\",\"status\":\"{nextSim}\"}}");
		}

		if (!TryResolveCredentials(shipment, credential, out string cariKodu, out string sifre, out string? missing))
		{
			return ProviderShipmentResult.Fail(missing!, "{\"error\":\"missing_credentials\"}");
		}

		string webOrderCode = ResolveWebSiparisKodu(shipment);
		try
		{
			return UsesSoap(credential)
				? await TrackViaSoapAsync(shipment, credential, cariKodu, sifre, webOrderCode, cancellationToken).ConfigureAwait(false)
				: await TrackViaRestAsync(shipment, credential, cariKodu, sifre, webOrderCode, cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Surat Kargo durum istegi hata {Ref}", shipment.ShipmentReference);
			return ProviderShipmentResult.Fail("Surat Kargo durum baglanti hatasi: " + ex.Message);
		}
	}

	private async Task<ProviderShipmentResult> CreateViaRestAsync(
		CargoShipment shipment,
		ProviderCredential? credential,
		string cariKodu,
		string sifre,
		SuratGonderiPayload payload,
		CancellationToken cancellationToken)
	{
		string url = $"{ResolveRestBaseUrl(credential)}/api/GonderiyiKargoyaGonder/GonderiBarkodOlustur?CariKodu={Uri.EscapeDataString(cariKodu)}&Sifre={Uri.EscapeDataString(sifre)}";
		using HttpClient http = CreateClient();
		using StringContent content = new(JsonSerializer.Serialize(payload.Body), Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await http.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
		string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Sürat Kargo gonderi olusturma basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
			return ProviderShipmentResult.Fail($"Sürat Kargo API hatasi (HTTP {response.StatusCode}).", raw);
		}

		string? error = ExtractErrorMessage(raw);
		if (error is not null)
		{
			_logger.LogWarning("Sürat Kargo API hata dondu {Ref}: {Error}", shipment.ShipmentReference, error);
			return ProviderShipmentResult.Fail("Sürat Kargo hatasi: " + error, raw);
		}

		string finalTracking = ExtractTrackingNumber(raw) ?? payload.OzelKargoTakipNo;
		string? takipUrl = ExtractString(raw, "TakipUrl", "takipUrl");
		_logger.LogInformation("Sürat Kargo gonderisi olusturuldu {Ref}. Takip: {Tracking}", shipment.ShipmentReference, finalTracking);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, finalTracking, takipUrl, null, "Surat Kargo gonderisi olusturuldu.", raw);
	}

	private async Task<ProviderShipmentResult> CreateViaSoapAsync(
		CargoShipment shipment,
		ProviderCredential? credential,
		string cariKodu,
		string sifre,
		SuratGonderiPayload payload,
		CancellationToken cancellationToken)
	{
		string endpoint = ResolveSoapEndpoint(credential);
		string xml = BuildCreateSoapEnvelope(cariKodu, sifre, payload);
		using HttpClient http = CreateClient();
		using StringContent content = new(xml, Encoding.UTF8, "text/xml");
		content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
		content.Headers.Add("SOAPAction", "\"http://tempuri.org/GonderiyiKargoyaGonderYeni\"");
		using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
		string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			_logger.LogWarning("Sürat Kargo SOAP gonderi basarisiz {Ref}. HTTP {Status}: {Body}", shipment.ShipmentReference, (int)response.StatusCode, raw);
			return ProviderShipmentResult.Fail($"Sürat Kargo SOAP hatasi (HTTP {response.StatusCode}).", raw);
		}

		string? result = ExtractSoapResult(raw, "GonderiyiKargoyaGonderYeniResult", "GonderiyiKargoyaGonderyeniResult");
		if (!IsSoapCreateSuccess(result))
		{
			_logger.LogWarning("Sürat Kargo SOAP hata dondu {Ref}: {Result}", shipment.ShipmentReference, result);
			return ProviderShipmentResult.Fail("Sürat Kargo hatasi: " + (result ?? "bos SOAP sonucu"), raw);
		}

		_logger.LogInformation("Sürat Kargo SOAP gonderisi olusturuldu {Ref}. Takip: {Tracking}", shipment.ShipmentReference, payload.OzelKargoTakipNo);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, payload.OzelKargoTakipNo, null, null, "Surat Kargo gonderisi olusturuldu (" + result + ").", raw);
	}

	private async Task<ProviderShipmentResult> TrackViaRestAsync(
		CargoShipment shipment,
		ProviderCredential? credential,
		string cariKodu,
		string sifre,
		string webOrderCode,
		CancellationToken cancellationToken)
	{
		string url = $"{ResolveRestBaseUrl(credential)}/api/KargoTakipHareketDetayi?CariKodu={Uri.EscapeDataString(cariKodu)}&Sifre={Uri.EscapeDataString(sifre)}&WebSiparisKodu={Uri.EscapeDataString(webOrderCode)}";
		using HttpClient http = CreateClient();
		using HttpRequestMessage request = new(HttpMethod.Post, url);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
		string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			return ProviderShipmentResult.Fail($"Surat Kargo durum sorgu hatasi (HTTP {response.StatusCode}).", raw);
		}

		return MapTrackResponse(shipment, raw);
	}

	private async Task<ProviderShipmentResult> TrackViaSoapAsync(
		CargoShipment shipment,
		ProviderCredential? credential,
		string cariKodu,
		string sifre,
		string webOrderCode,
		CancellationToken cancellationToken)
	{
		string endpoint = ResolveSoapEndpoint(credential);
		string xml = BuildTrackSoapEnvelope(cariKodu, sifre, webOrderCode);
		using HttpClient http = CreateClient();
		using StringContent content = new(xml, Encoding.UTF8, "text/xml");
		content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
		content.Headers.Add("SOAPAction", "\"http://tempuri.org/KargoTakipHareketDetayi\"");
		using HttpResponseMessage response = await http.PostAsync(endpoint, content, cancellationToken).ConfigureAwait(false);
		string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		if (!response.IsSuccessStatusCode)
		{
			return ProviderShipmentResult.Fail($"Surat Kargo durum SOAP hatasi (HTTP {response.StatusCode}).", raw);
		}

		string? inner = ExtractSoapResult(raw, "KargoTakipHareketDetayiResult");
		return MapTrackResponse(shipment, string.IsNullOrWhiteSpace(inner) ? raw : inner);
	}

	private ProviderShipmentResult MapTrackResponse(CargoShipment shipment, string raw)
	{
		string? error = ExtractErrorMessage(raw);
		if (error is not null)
		{
			return ProviderShipmentResult.Fail("Surat Kargo durum hatasi: " + error, raw);
		}

		ShipmentStatus status = MapSuratStatus(raw, shipment.Status);
		string tracking = ExtractTrackingNumber(raw) ?? shipment.TrackingNumber;
		string? takipUrl = ExtractString(raw, "TakipUrl", "takipUrl") ?? shipment.LabelUrl;
		string? durum = ExtractString(raw, "KargonunDurumu", "kargonunDurumu");
		_logger.LogInformation("Surat Kargo durum guncellendi {Ref}: {Status}", shipment.ShipmentReference, status);
		string message = string.IsNullOrWhiteSpace(durum)
			? $"Surat Kargo durum: {status}."
			: $"Surat Kargo durum: {durum} ({status}).";
		return ProviderShipmentResult.Ok(status, tracking, takipUrl, null, message, raw);
	}

	internal static SuratGonderiPayload BuildGonderi(CargoShipment shipment, ProviderCredential? credential)
	{
		LegacyPaymentKind paymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		bool isCod = paymentKind != LegacyPaymentKind.Prepaid;
		int packageCount = LegacyProviderMappingHelpers.GetPackageCount(shipment);
		byte kargoTuru = ParseByte(GetSetting(credential, "packageType")).GetValueOrDefault(2);
		string ozelTakipNo = Truncate(ResolveWebSiparisKodu(shipment), 50);
		string aliciAdresi = LegacyProviderMappingHelpers.ResolveAddress(shipment.Recipient);
		string il = LegacyProviderMappingHelpers.ResolveCity(shipment.Recipient);
		string ilce = LegacyProviderMappingHelpers.ResolveDistrict(shipment.Recipient);
		string kisiKurum = LegacyProviderMappingHelpers.ResolveRecipientName(shipment);
		string telefonCep = LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone);
		if (string.IsNullOrWhiteSpace(kisiKurum))
		{
			return new SuratGonderiPayload { ValidationError = "Sürat Kargo: KisiKurum (alıcı adı) zorunludur." };
		}
		if (string.IsNullOrWhiteSpace(aliciAdresi))
		{
			return new SuratGonderiPayload { ValidationError = "Sürat Kargo: AliciAdresi zorunludur." };
		}
		if (string.IsNullOrWhiteSpace(il) || string.IsNullOrWhiteSpace(ilce))
		{
			return new SuratGonderiPayload { ValidationError = "Sürat Kargo: Il ve Ilce zorunludur." };
		}
		if (string.IsNullOrWhiteSpace(telefonCep) || telefonCep.Length < 10)
		{
			return new SuratGonderiPayload { ValidationError = "Sürat Kargo: TelefonCep en az 10 haneli olmalıdır." };
		}

		decimal birimDesi = UnitMeasure(shipment, p => p.Desi, 1m);
		decimal birimKg = UnitMeasure(shipment, p => p.Weight, 1m);
		if (kargoTuru == 1)
		{
			birimDesi = 0;
			birimKg = 0;
		}
		else if (kargoTuru == 2)
		{
			birimDesi = 1;
			birimKg = 1;
		}

		int odemeTipi = LegacyProviderMappingHelpers.RecipientPaysShipping(shipment) ? 2 : 1;
		byte teslimSekli = ParseByte(GetSetting(credential, "teslimSekli") ?? LegacyProviderMappingHelpers.GetMetadataValue(shipment, "surat.teslimSekli", "teslimSekli")).GetValueOrDefault(1);
		byte tasimaSekli = ParseByte(GetSetting(credential, "tasimaSekli") ?? LegacyProviderMappingHelpers.GetMetadataValue(shipment, "surat.tasimaSekli")).GetValueOrDefault(1);
		byte gonderiSekli = ParseByte(GetSetting(credential, "gonderiSekli") ?? LegacyProviderMappingHelpers.GetMetadataValue(shipment, "surat.gonderiSekli")).GetValueOrDefault(0);
		bool iadeMi = LegacyProviderMappingHelpers.IsTrue(LegacyProviderMappingHelpers.GetMetadataValue(shipment, "return", "iade", "iademi"));
		string? entegrasyonFirmasi = ResolveMarketplace(shipment);
		byte pazarYeriMi = (byte)(entegrasyonFirmasi is null ? 0 : 1);
		string? irsaliyeSeri = Truncate(LegacyProviderMappingHelpers.GetMetadataValue(shipment, "invoice.serial", "invoiceSerial"), 5);
		string? irsaliyeSira = Truncate(LegacyProviderMappingHelpers.GetMetadataValue(shipment, "invoice.sequence", "invoiceSequence"), 10);
		if (isCod && (string.IsNullOrWhiteSpace(irsaliyeSeri) || string.IsNullOrWhiteSpace(irsaliyeSira)))
		{
			return new SuratGonderiPayload { ValidationError = "Sürat Kargo kapıdan ödeme için IrsaliyeSeriNo ve IrsaliyeSiraNo zorunludur." };
		}

		var body = new Dictionary<string, object?>
		{
			["KisiKurum"] = kisiKurum,
			["SahisBirim"] = LegacyProviderMappingHelpers.BuildPackageContent(shipment),
			["AliciAdresi"] = aliciAdresi,
			["Il"] = il,
			["Ilce"] = ilce,
			["TelefonEv"] = NullIfEmpty(LegacyProviderMappingHelpers.NormalizePhone(shipment.Recipient.Phone)),
			["TelefonCep"] = telefonCep,
			["Email"] = NullIfEmpty(shipment.Recipient.Email),
			["KargoTuru"] = kargoTuru,
			["Odemetipi"] = odemeTipi,
			["ReferansNo"] = Truncate(shipment.OrderReference, 50),
			["OzelKargoTakipNo"] = ozelTakipNo,
			["Adet"] = packageCount,
			["BirimDesi"] = birimDesi,
			["BirimKg"] = birimKg,
			["KargoIcerigi"] = BuildKargoIcerigi(shipment, kargoTuru),
			["TasimaSekli"] = tasimaSekli,
			["TeslimSekli"] = teslimSekli,
			["GonderiSekli"] = gonderiSekli,
			["Pazaryerimi"] = pazarYeriMi,
			["Iademi"] = (byte)(iadeMi ? 1 : 0)
		};

		if (!string.IsNullOrWhiteSpace(irsaliyeSeri))
		{
			body["IrsaliyeSeriNo"] = irsaliyeSeri;
		}
		if (!string.IsNullOrWhiteSpace(irsaliyeSira))
		{
			body["IrsaliyeSiraNo"] = irsaliyeSira;
		}
		if (pazarYeriMi == 1)
		{
			body["EntegrasyonFirmasi"] = entegrasyonFirmasi;
		}

		string? teslimSube = GetSetting(credential, "teslimSubeKodu") ?? LegacyProviderMappingHelpers.GetMetadataValue(shipment, "surat.teslimSubeKodu", "teslimSubeKodu");
		if (!string.IsNullOrWhiteSpace(teslimSube))
		{
			body["TeslimSubeKodu"] = teslimSube;
		}

		if (isCod)
		{
			body["KapidanOdemeTutari"] = LegacyProviderMappingHelpers.ResolveCollectionAmount(shipment);
			body["KapidanOdemeTahsilatTipi"] = paymentKind == LegacyPaymentKind.CashOnDeliveryCard ? 2 : 1;
		}

		string? ekHizmetler = GetSetting(credential, "ekHizmetler");
		if (!string.IsNullOrWhiteSpace(ekHizmetler))
		{
			body["EkHizmetler"] = ekHizmetler;
		}

		return new SuratGonderiPayload
		{
			OzelKargoTakipNo = ozelTakipNo,
			Body = body,
			IsCod = isCod
		};
	}

	internal static ShipmentStatus MapDurumSayi(int code, ShipmentStatus current) => code switch
	{
		1 => ShipmentStatus.ProviderAccepted,
		2 or 3 or 4 or 5 or 8 => ShipmentStatus.InTransit,
		6 or 7 => ShipmentStatus.Delivered,
		9 or 10 or 11 or 13 or 14 or 15 or 16 => ShipmentStatus.InTransit,
		12 => ShipmentStatus.Delivered,
		_ => current
	};

	internal static bool IsSoapCreateSuccess(string? result)
	{
		if (string.IsNullOrWhiteSpace(result))
		{
			return false;
		}

		string trimmed = result.Trim();
		if (trimmed.Equals("Tamam", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		foreach (string token in new[] { "010", "011", "012", "013", "014", "015", "016" })
		{
			if (trimmed.Contains(token, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	private static string BuildKargoIcerigi(CargoShipment shipment, byte kargoTuru)
	{
		IReadOnlyList<Kargoyeri.Domain.ValueObjects.PackageInfo> packages = shipment.Packages.Count == 0
			? new[] { new Kargoyeri.Domain.ValueObjects.PackageInfo { PackageSequence = 1, Desi = 1, Weight = 1 } }
			: shipment.Packages;
		var sb = new StringBuilder();
		foreach (var package in packages)
		{
			decimal desi = package.Desi > 0 ? package.Desi : 1m;
			decimal kg = package.Weight > 0 ? package.Weight : 1m;
			sb.Append(desi.ToString("0.##", CultureInfo.InvariantCulture))
				.Append(':')
				.Append(kg.ToString("0.##", CultureInfo.InvariantCulture))
				.Append(':')
				.Append(kargoTuru)
				.Append(":1;");
		}
		return sb.ToString();
	}

	private static decimal UnitMeasure(CargoShipment shipment, Func<Kargoyeri.Domain.ValueObjects.PackageInfo, decimal> selector, decimal fallback)
	{
		if (shipment.Packages.Count == 0)
		{
			return fallback;
		}

		decimal first = selector(shipment.Packages[0]);
		return first > 0 ? first : fallback;
	}

	private static string? ResolveMarketplace(CargoShipment shipment)
	{
		string? explicitFirm = LegacyProviderMappingHelpers.GetMetadataValue(shipment, "surat.entegrasyonFirmasi", "entegrasyonFirmasi");
		if (!string.IsNullOrWhiteSpace(explicitFirm))
		{
			return explicitFirm;
		}

		return shipment.SourceChannel switch
		{
			OrderSourceChannel.Trendyol => "Trendyol",
			OrderSourceChannel.Hepsiburada => "Hepsiburada",
			OrderSourceChannel.N11 => "N11",
			OrderSourceChannel.Pazarama => "Pazarama",
			_ => null
		};
	}

	private static bool TryResolveCredentials(CargoShipment shipment, ProviderCredential? credential, out string cariKodu, out string sifre, out string? error)
	{
		LegacyPaymentKind paymentKind = LegacyProviderMappingHelpers.ResolvePaymentKind(shipment);
		bool isCod = paymentKind != LegacyPaymentKind.Prepaid;
		cariKodu = FirstNonEmpty(
			isCod ? GetSetting(credential, "usernameCod") : null,
			credential?.ClientCode,
			credential?.Username) ?? string.Empty;
		sifre = FirstNonEmpty(
			isCod ? GetSetting(credential, "panelPasswordCod") : null,
			isCod ? GetSetting(credential, "passwordCod") : null,
			credential?.Password,
			GetSetting(credential, "panelPassword")) ?? string.Empty;
		if (string.IsNullOrWhiteSpace(cariKodu) || string.IsNullOrWhiteSpace(sifre))
		{
			error = "Sürat Kargo kimlik bilgileri eksik (CariKodu/ClientCode ve Sifre/Password).";
			return false;
		}

		error = null;
		return true;
	}

	private static string ResolveWebSiparisKodu(CargoShipment shipment)
	{
		return FirstNonEmpty(shipment.TrackingNumber, shipment.ClientShipmentReference, shipment.ShipmentReference, shipment.OrderReference)
			?? shipment.ShipmentReference;
	}

	private static bool UsesSoap(ProviderCredential? credential)
	{
		string transport = GetSetting(credential, "transport") ?? string.Empty;
		if (transport.Equals("soap", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (transport.Equals("rest", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string endpoint = (credential?.EndpointBase ?? string.Empty).ToLowerInvariant();
		return endpoint.Contains("webservices.suratkargo.com.tr", StringComparison.Ordinal)
			|| endpoint.Contains("prova.suratkargo.com.tr", StringComparison.Ordinal)
			|| endpoint.Contains("services.asmx", StringComparison.Ordinal);
	}

	private static string ResolveRestBaseUrl(ProviderCredential? credential)
	{
		if (UsesSoap(credential))
		{
			string soap = (credential?.EndpointBase ?? string.Empty).ToLowerInvariant();
			if (soap.Contains("prova.suratkargo.com.tr", StringComparison.Ordinal))
			{
				return DefaultRestBaseUrl;
			}
			if (soap.Contains("webservices.suratkargo.com.tr", StringComparison.Ordinal))
			{
				return LiveRestBaseUrl;
			}
		}

		string? configured = credential?.EndpointBase;
		if (string.IsNullOrWhiteSpace(configured) || UsesSoap(credential))
		{
			return DefaultRestBaseUrl;
		}

		return configured.TrimEnd('/');
	}

	private static string ResolveSoapEndpoint(ProviderCredential? credential)
	{
		string? configured = credential?.EndpointBase;
		if (string.IsNullOrWhiteSpace(configured))
		{
			return DefaultSoapEndpoint;
		}

		string trimmed = configured.TrimEnd('/');
		if (trimmed.Contains("api0", StringComparison.OrdinalIgnoreCase))
		{
			return trimmed.Contains("api02", StringComparison.OrdinalIgnoreCase) ? TestSoapEndpoint : DefaultSoapEndpoint;
		}

		return trimmed.Contains("services.asmx", StringComparison.OrdinalIgnoreCase)
			? trimmed
			: trimmed + "/services.asmx";
	}

	private HttpClient CreateClient()
	{
		HttpClient http = _httpClientFactory.CreateClient();
		http.DefaultRequestHeaders.Accept.Clear();
		http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		return http;
	}

	private static bool IsSimulation(ProviderCredential? credential) => IsTrue(GetSetting(credential, "simulationMode"));

	private ProviderShipmentResult SimulateCreate(CargoShipment shipment, ProviderCredential? credential)
	{
		ProviderRequestPreviewResponse? preview = _blueprints.FirstOrDefault(x => x.SupportedProvider == CargoProviderType.Surat)?.PreviewCreateShipment(shipment, credential);
		string tracking = preview?.TrackingNumberCandidate ?? ResolveWebSiparisKodu(shipment);
		_logger.LogInformation("Surat Kargo simulasyon gonderisi olusturuldu {Ref}. Takip: {Tracking}", shipment.ShipmentReference, tracking);
		return ProviderShipmentResult.Ok(ShipmentStatus.ProviderAccepted, tracking, null, null, "Surat Kargo simulasyon gonderisi olusturuldu.", preview?.PayloadPreview);
	}

	private static ShipmentStatus SimulateNextStatus(CargoShipment shipment) => shipment.Status switch
	{
		ShipmentStatus.Pending => ShipmentStatus.ProviderAccepted,
		ShipmentStatus.ProviderAccepted => ShipmentStatus.InTransit,
		ShipmentStatus.InTransit when shipment.RetryCount >= 2 => ShipmentStatus.Delivered,
		_ => shipment.Status
	};

	private static string? GetSetting(ProviderCredential? credential, string key)
	{
		if (credential?.AdditionalSettings is null)
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

	private static bool IsTrue(string? value) =>
		value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase));

	private static byte? ParseByte(string? value) => byte.TryParse(value, out byte result) ? result : null;

	private static string? FirstNonEmpty(params string?[] values) =>
		values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

	private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

	private static string Truncate(string? value, int max)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		value = value.Trim();
		return value.Length <= max ? value : value[..max];
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
			JsonElement root = jsonDocument.RootElement;
			if (TryGetProperty(root, out JsonElement isError, "IsError", "isError") &&
			    (isError.ValueKind == JsonValueKind.True || (isError.ValueKind == JsonValueKind.String && IsTrue(isError.GetString()))))
			{
				return ExtractString(raw, "errorMessage", "ErrorMessage", "Message", "Hata") ?? "Sürat Kargo IsError=true.";
			}

			foreach (string propertyName in new[] { "ErrorMessage", "errorMessage", "Message", "message", "Hata", "hata", "Error", "error" })
			{
				if (root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
				{
					string? text = value.GetString();
					if (!string.IsNullOrWhiteSpace(text) && !text.Equals("Tamam", StringComparison.OrdinalIgnoreCase))
					{
						return text;
					}
				}
			}

			foreach (string propertyName in new[] { "IsSuccess", "isSuccess", "Success", "success" })
			{
				if (root.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.False)
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
		string? direct = ExtractString(raw, "KargoTakipNo", "kargoTakipNo", "KargoNo", "BarkodNo", "TrackingNumber", "OzelKargoTakipNo", "Satiskodu");
		if (!string.IsNullOrWhiteSpace(direct))
		{
			return direct;
		}

		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}

		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			if (TryGetGonderi(jsonDocument.RootElement, out JsonElement gonderi))
			{
				return ExtractTrackingNumber(gonderi.GetRawText());
			}
		}
		catch
		{
		}

		return null;
	}

	internal static ShipmentStatus MapSuratStatus(string? raw, ShipmentStatus current)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return current;
		}

		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement root = jsonDocument.RootElement;
			if (TryGetGonderi(root, out JsonElement gonderi))
			{
				root = gonderi;
			}

			if (TryGetProperty(root, out JsonElement sayiEl, "KargonunDurumuSayi", "kargonunDurumuSayi") &&
			    TryReadInt(sayiEl, out int sayi))
			{
				return MapDurumSayi(sayi, current);
			}

			string text = (ExtractString(root.GetRawText(), "KargonunDurumu", "Durum", "Status", "LastStatus", "Islem") ?? string.Empty).ToLowerInvariant();
			if (text.Contains("teslim edildi") || text.Contains("delivered") || text.Contains("mgt teslim"))
			{
				return ShipmentStatus.Delivered;
			}
			if (text.Contains("iptal") || text.Contains("cancel"))
			{
				return ShipmentStatus.Cancelled;
			}
			if (text.Contains("iade"))
			{
				return ShipmentStatus.InTransit;
			}
			if (text.Contains("hazirlaniyor") || text.Contains("evrak") || text.Contains("kabul"))
			{
				return ShipmentStatus.ProviderAccepted;
			}
			if (text.Contains("dagitim") || text.Contains("yolda") || text.Contains("transfer") || text.Contains("sube") || text.Contains("hareket") || text.Contains("kurye"))
			{
				return ShipmentStatus.InTransit;
			}
		}
		catch
		{
		}

		return current;
	}

	private static string? ExtractString(string? raw, params string[] names)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return null;
		}

		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(raw);
			JsonElement root = jsonDocument.RootElement;
			if (TryGetGonderi(root, out JsonElement gonderi))
			{
				root = gonderi;
			}

			foreach (string name in names)
			{
				if (TryGetProperty(root, out JsonElement value, name) && value.ValueKind == JsonValueKind.String)
				{
					string? text = value.GetString();
					if (!string.IsNullOrWhiteSpace(text))
					{
						return text;
					}
				}
			}
		}
		catch
		{
		}

		return null;
	}

	private static bool TryGetGonderi(JsonElement root, out JsonElement gonderi)
	{
		if (TryGetProperty(root, out JsonElement list, "Gonderiler", "gonderiler") &&
		    list.ValueKind == JsonValueKind.Array &&
		    list.GetArrayLength() > 0)
		{
			gonderi = list[0];
			return true;
		}

		if (TryGetProperty(root, out JsonElement data, "Data", "data"))
		{
			gonderi = data;
			return true;
		}

		gonderi = default;
		return false;
	}

	private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
	{
		foreach (string name in names)
		{
			if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out value))
			{
				return true;
			}
		}

		value = default;
		return false;
	}

	private static bool TryReadInt(JsonElement element, out int value)
	{
		if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
		{
			return true;
		}

		if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value))
		{
			return true;
		}

		value = 0;
		return false;
	}

	private static string BuildCreateSoapEnvelope(string cariKodu, string sifre, SuratGonderiPayload payload)
	{
		var gonderi = new XElement(XName.Get("Gonderi", SoapNs));
		foreach (KeyValuePair<string, object?> pair in payload.Body)
		{
			if (pair.Value is null)
			{
				continue;
			}

			string text = pair.Key is "KapidanOdemeTutari"
				? Convert.ToDecimal(pair.Value, CultureInfo.InvariantCulture).ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"))
				: Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty;
			gonderi.Add(new XElement(XName.Get(pair.Key, SoapNs), text));
		}

		XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
		var envelope = new XDocument(
			new XDeclaration("1.0", "utf-8", null),
			new XElement(soap + "Envelope",
				new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
				new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
				new XAttribute(XNamespace.Xmlns + "soap", soap.NamespaceName),
				new XElement(soap + "Body",
					new XElement(XName.Get("GonderiyiKargoyaGonderYeni", SoapNs),
						new XElement(XName.Get("KullaniciAdi", SoapNs), cariKodu),
						new XElement(XName.Get("Sifre", SoapNs), sifre),
						gonderi))));
		return envelope.Declaration + Environment.NewLine + envelope;
	}

	private static string BuildTrackSoapEnvelope(string cariKodu, string sifre, string webSiparisKodu)
	{
		XNamespace soap = "http://schemas.xmlsoap.org/soap/envelope/";
		var envelope = new XDocument(
			new XDeclaration("1.0", "utf-8", null),
			new XElement(soap + "Envelope",
				new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
				new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
				new XAttribute(XNamespace.Xmlns + "soap", soap.NamespaceName),
				new XElement(soap + "Body",
					new XElement(XName.Get("KargoTakipHareketDetayi", SoapNs),
						new XElement(XName.Get("CariKodu", SoapNs), cariKodu),
						new XElement(XName.Get("Sifre", SoapNs), sifre),
						new XElement(XName.Get("WebSiparisKodu", SoapNs), webSiparisKodu)))));
		return envelope.Declaration + Environment.NewLine + envelope;
	}

	private static string? ExtractSoapResult(string raw, params string[] localNames)
	{
		try
		{
			XDocument document = XDocument.Parse(raw);
			foreach (string name in localNames)
			{
				string? value = document.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value?.Trim();
				if (!string.IsNullOrWhiteSpace(value))
				{
					return value;
				}
			}
		}
		catch
		{
		}

		return null;
	}
}

internal sealed class SuratGonderiPayload
{
	public string OzelKargoTakipNo { get; init; } = string.Empty;
	public Dictionary<string, object?> Body { get; init; } = new();
	public bool IsCod { get; init; }
	public string? ValidationError { get; init; }
}
