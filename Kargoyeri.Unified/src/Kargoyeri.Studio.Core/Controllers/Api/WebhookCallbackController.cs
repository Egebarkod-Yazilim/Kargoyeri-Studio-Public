using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers.Api;

/// <summary>
/// Kargo firmalari (Yurtici, MNG, Surat, Aras, PTT, UPS) bizim sistemimize gonderi
/// durum guncellemelerini POST etmek icin bu uca cagri yapar.
///
/// URL kalibi:  POST /api/webhook/{provider}/{tenantKey}
///
/// Auth (P1-#1): HMAC-SHA256 imza dogrulamasi.
///   Header: X-Kargoyeri-Signature: t=&lt;unix-seconds&gt;,v1=&lt;hex(hmac_sha256(secret, "t.body"))&gt;
///   Secret: tenant Metadata["webhook.inbound.secret"]
///   Replay penceresi: 5 dakika (timestamp simdi'ye gore +-300 sn icinde olmali).
///
/// Geriye donuk uyumluluk: Eger X-Kargoyeri-Signature gonderilmemisse ve eski
/// X-Kargoyeri-Inbound-Secret header'i mevcutsa shared-secret esitligi denenir;
/// bu yol kullanildiginda warning log yazilir (gecis donemi — sonra kaldirilacak).
///
/// Davranis: Payload'dan shipmentReference / trackingNumber cikarilir, ilgili
///           gonderi RefreshShipmentAsync ile provider'dan dogrulanir (bizim
///           sistemimiz inbound payload'a koru korune guvenmez — sadece bilgi
///           tetikleyicisi olarak kullanir).
/// </summary>
[ApiController]
[Route("api/webhook/{provider}/{tenantKey}")]
[AllowAnonymous]
public sealed class WebhookCallbackController : ControllerBase
{
    private const string LegacySecretHeader  = "X-Kargoyeri-Inbound-Secret";
    private const string SignatureHeader     = "X-Kargoyeri-Signature";
    private const string InboundSecretMetaKey = "webhook.inbound.secret";

    /// <summary>HMAC timestamp tolerance (anti-replay window) — 5 dakika.</summary>
    private const int ReplayToleranceSeconds = 300;

    private readonly CargoOrchestrator _orchestrator;
    private readonly CustomerService   _customerService;
    private readonly ILogger<WebhookCallbackController> _logger;

    public WebhookCallbackController(
        CargoOrchestrator orchestrator,
        CustomerService customerService,
        ILogger<WebhookCallbackController> logger)
    {
        _orchestrator    = orchestrator;
        _customerService = customerService;
        _logger          = logger;
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Receive(
        string provider,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CargoProviderTypeDto>(provider, ignoreCase: true, out var providerType))
            return BadRequest(new { error = $"Bilinmeyen kargo firmasi: {provider}" });

        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken);
        if (profile is null) return NotFound(new { error = "Tenant bulunamadi." });
        if (!profile.IsActive) return StatusCode(StatusCodes.Status423Locked, new { error = "Tenant pasif." });

        // Provider tenant icin yetkili mi?
        if (!profile.AllowedProviders.Contains(providerType))
            return Forbid();

        if (!profile.Metadata.TryGetValue(InboundSecretMetaKey, out var expectedSecret)
            || string.IsNullOrWhiteSpace(expectedSecret))
        {
            _logger.LogWarning("Webhook reddedildi: tenant {TenantKey} icin inbound secret tanimli degil.", tenantKey);
            return Unauthorized(new { error = "Inbound webhook secret tanimsiz. Yoneticinize basvurun." });
        }

        // Body'yi RAW olarak oku — HMAC body uzerinden hesaplanir, tekrar parse edilebilmesi icin
        // string'e aliyoruz (small payload — webhook kargo statu mesajidir).
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { error = "Bos govde." });

        // 1) Yeni HMAC imza varsa onu dogrula.
        var signatureHeader = Request.Headers[SignatureHeader].ToString();
        if (!string.IsNullOrWhiteSpace(signatureHeader))
        {
            var verify = VerifyHmacSignature(signatureHeader, body, expectedSecret);
            if (!verify.Ok)
            {
                _logger.LogWarning("Webhook reddedildi (HMAC): {Reason} tenant={TenantKey} provider={Provider}",
                    verify.Reason, tenantKey, provider);
                return Unauthorized(new { error = "Gecersiz imza: " + verify.Reason });
            }
        }
        else
        {
            // 2) Geriye donuk: eski shared-secret yolu.
            var legacy = Request.Headers[LegacySecretHeader].ToString();
            if (string.IsNullOrEmpty(legacy) || !FixedTimeStringEquals(legacy, expectedSecret))
            {
                _logger.LogWarning("Webhook reddedildi: imza/secret yok veya gecersiz. tenant={TenantKey} provider={Provider}",
                    tenantKey, provider);
                return Unauthorized(new { error = "Gecersiz inbound secret veya imza eksik." });
            }
            _logger.LogWarning(
                "Webhook DEPRECATED shared-secret yolu kullanildi (tenant={TenantKey} provider={Provider}). " +
                "X-Kargoyeri-Signature HMAC formatina gecilmesi gerekir.",
                tenantKey, provider);
        }

        // Payload parse — esnek: shipmentReference / trackingNumber / status
        string? shipmentReference;
        string? trackingNumber;
        string? rawStatus;
        try
        {
            using var doc = JsonDocument.Parse(body);
            shipmentReference = TryExtractString(doc.RootElement, "shipmentReference", "shipment_reference", "reference", "orderRef", "musteriRef");
            trackingNumber    = TryExtractString(doc.RootElement, "trackingNumber", "tracking_number", "kargoNo", "tracking", "barcode");
            rawStatus         = TryExtractString(doc.RootElement, "status", "durum", "state", "eventType");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Webhook payload JSON degil. tenant={TenantKey} provider={Provider}", tenantKey, provider);
            return BadRequest(new { error = "JSON parse hatasi: " + ex.Message });
        }

        if (string.IsNullOrWhiteSpace(shipmentReference) && string.IsNullOrWhiteSpace(trackingNumber))
            return BadRequest(new { error = "shipmentReference veya trackingNumber alanlari zorunludur." });

        if (string.IsNullOrWhiteSpace(shipmentReference))
        {
            var all = await _orchestrator.ListShipmentsAsync(tenantKey, cancellationToken);
            var hit = all.FirstOrDefault(s =>
                string.Equals(s.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase));
            if (hit is null)
                return NotFound(new { error = $"trackingNumber={trackingNumber} icin gonderi bulunamadi." });
            shipmentReference = hit.ShipmentReference;
        }

        try
        {
            var refreshed = await _orchestrator.RefreshShipmentAsync(shipmentReference!, tenantKey, cancellationToken);
            _logger.LogInformation(
                "Webhook callback isledi: tenant={TenantKey} provider={Provider} shipmentRef={Ref} status={Status} (provider raw status={Raw})",
                tenantKey, provider, shipmentReference, refreshed.Status, rawStatus);

            return Ok(new
            {
                accepted          = true,
                shipmentReference = refreshed.ShipmentReference,
                status            = refreshed.Status,
                trackingNumber    = refreshed.TrackingNumber,
                providerRawStatus = rawStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook callback isleme hatasi. tenant={TenantKey} provider={Provider} ref={Ref}",
                tenantKey, provider, shipmentReference);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Iceride hata olustu, daha sonra tekrar deneyin." });
        }
    }

    // ---------------------------------------------------------------------
    //  HMAC verification
    // ---------------------------------------------------------------------

    private readonly record struct HmacResult(bool Ok, string Reason)
    {
        public static HmacResult Success { get; } = new(true, "");
        public static HmacResult Fail(string r) => new(false, r);
    }

    /// <summary>
    /// Header format:  t=&lt;unix-seconds&gt;,v1=&lt;hex(hmac_sha256(secret, "t.body"))&gt;
    /// Stripe-uyumlu. Birden fazla v1 imzasi gelirse (rotation icin) hepsi denenir.
    /// </summary>
    private static HmacResult VerifyHmacSignature(string headerValue, string rawBody, string secret)
    {
        long? timestamp = null;
        var v1Signatures = new List<string>();

        foreach (var part in headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq < 0) continue;
            var key = part[..eq].Trim();
            var val = part[(eq + 1)..].Trim();
            if (key.Equals("t", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(val, out var ts)) timestamp = ts;
            }
            else if (key.Equals("v1", StringComparison.OrdinalIgnoreCase))
            {
                v1Signatures.Add(val);
            }
        }

        if (timestamp is null) return HmacResult.Fail("timestamp (t) eksik");
        if (v1Signatures.Count == 0) return HmacResult.Fail("v1 imza eksik");

        var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var drift  = Math.Abs(nowSec - timestamp.Value);
        if (drift > ReplayToleranceSeconds)
            return HmacResult.Fail($"timestamp pencere disinda ({drift}sn > {ReplayToleranceSeconds}sn)");

        var signedPayload = $"{timestamp.Value}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var computedHex = Convert.ToHexString(computed);

        foreach (var candidate in v1Signatures)
        {
            // Hex karsilastirmasi — case-insensitive olabilmesi icin upper'a normalize
            if (candidate.Length != computedHex.Length) continue;
            var candidateBytes = Encoding.ASCII.GetBytes(candidate.ToUpperInvariant());
            var computedBytes  = Encoding.ASCII.GetBytes(computedHex);
            if (CryptographicOperations.FixedTimeEquals(candidateBytes, computedBytes))
                return HmacResult.Success;
        }

        return HmacResult.Fail("v1 imza eslesmedi");
    }

    private static bool FixedTimeStringEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    private static string? TryExtractString(JsonElement root, params string[] propertyNames)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in propertyNames)
        {
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        return prop.Value.GetString();
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                        return prop.Value.GetRawText();
                }
            }
        }
        return null;
    }
}
