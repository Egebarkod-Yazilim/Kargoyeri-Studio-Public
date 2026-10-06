using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Infrastructure.OrderChannels;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P5-#4 — Self-service signup akisi.
///
/// Adimlar:
///   1) GET  /signup           -> form (firma adi, tam ad, e-posta, telefon, VKN)
///   2) POST /signup           -> PendingSignupStore'a yaz, dogrulama maili gonder
///   3) GET  /signup/check     -> "e-postani kontrol et" sayfasi
///   4) GET  /signup/confirm/{token} -> token'i dogrula, tenant olustur, /onboarding'e yonlendir
/// </summary>
[AllowAnonymous]
[Route("signup")]
[EnableRateLimiting("signup")] // Program.cs'te tanimli olmali; yoksa attribute fail-open calisir
public sealed class SignupController : Controller
{
    private readonly PendingSignupStore _pending;
    private readonly IStudioEmailSender _email;
    private readonly CustomerService _customers;
    private readonly LinkGenerator _links;
    private readonly ILogger<SignupController> _logger;

    public SignupController(
        PendingSignupStore pending,
        IStudioEmailSender email,
        CustomerService customers,
        LinkGenerator links,
        ILogger<SignupController> logger)
    {
        _pending = pending;
        _email = email;
        _customers = customers;
        _links = links;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Kayıt Ol";
        return View(new SignupFormViewModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SignupFormViewModel form, CancellationToken ct)
    {
        ViewData["Title"] = "Kayıt Ol";
        if (!ModelState.IsValid) return View(form);

        // Mukerrer e-posta?
        var existing = _pending.List().Where(p => string.Equals(p.Email, form.Email.Trim(), StringComparison.OrdinalIgnoreCase) && !p.Confirmed && p.ExpiresAtUtc > DateTimeOffset.UtcNow).FirstOrDefault();
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(form.Email), "Bu e-posta icin acik bir kayit basvurusu zaten var. Lutfen e-postanizi kontrol edin.");
            return View(form);
        }

        var rec = _pending.Create(form.Email, form.CompanyName, form.FullName, form.Phone, form.Vkn,
            form.SelectedChannels, form.SelectedExtraSources);
        var confirmUrl = _links.GetUriByAction(HttpContext, action: nameof(Confirm), controller: "Signup", values: new { token = rec.Token });

        var body = $@"<p>Merhaba {WebUtilEncode(rec.FullName)},</p>
<p>{WebUtilEncode(rec.CompanyName)} adina Kargoyeri Studio kayit basvurunuzu aldik.</p>
<p>Hesabinizi etkinlestirmek icin asagidaki baglantiya tiklayin (24 saat gecerli):</p>
<p><a href=""{confirmUrl}"">{confirmUrl}</a></p>
<p>Bu kaydi siz olusturmadiysaniz bu e-postayi yok sayabilirsiniz.</p>
<hr/>
<p style=""color:#64748b;font-size:12px;"">Kargoyeri Studio &middot; Self-service signup</p>";

        try
        {
            await _email.SendAsync(rec.Email, "Kargoyeri Studio — Hesabinizi dogrulayin", body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signup mail send failed for {Email}", rec.Email);
            ModelState.AddModelError(string.Empty, "Dogrulama e-postasi gonderilemedi. Lutfen birazdan tekrar deneyin.");
            _pending.Remove(rec.Token);
            return View(form);
        }

        return RedirectToAction(nameof(Check), new { e = rec.Email });
    }

    [HttpGet("check")]
    public IActionResult Check(string? e)
    {
        ViewData["Title"] = "E-postanı Kontrol Et";
        ViewData["Email"] = e;
        return View();
    }

    [HttpGet("confirm/{token}")]
    public async Task<IActionResult> Confirm(string token, CancellationToken ct)
    {
        var rec = _pending.Find(token);
        if (rec is null)
        {
            ViewData["Title"] = "Dogrulama Hatasi";
            ViewData["Reason"] = "Token bulunamadi veya silinmis.";
            return View("ConfirmError");
        }
        if (rec.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            ViewData["Title"] = "Dogrulama Suresi Doldu";
            ViewData["Reason"] = "Dogrulama linki suresi 24 saatten uzun. Lutfen yeniden kayit olun.";
            return View("ConfirmError");
        }

        if (!_pending.Confirm(token))
        {
            ViewData["Reason"] = "Token dogrulanamadi.";
            return View("ConfirmError");
        }

        // Tenant olustur — kod = e-posta'nin local-part'i + kisa hash
        var localPart = rec.Email.Split('@')[0];
        var suffix = Math.Abs(rec.Email.GetHashCode()).ToString("X").Substring(0, 4);
        var tenantCode = SanitizeCode($"{localPart}-{suffix}");

        try
        {
            var customer = await _customers.EnsureCustomerAsync(
                customerCode: tenantCode,
                customerName: rec.CompanyName,
                notificationWebhook: null,
                notificationEmail: rec.Email,
                notificationPhone: rec.Phone,
                cancellationToken: ct);

            _logger.LogInformation("Signup confirmed: tenant {TenantKey} ({Company}) for {Email}",
                customer.TenantKey, rec.CompanyName, rec.Email);

            // Sihirbaz 2. adımında seçilen pazaryeri kanallarını metadata'ya işle
            // (Enabled=true; credential alanları sonra OrderChannels/Configure sayfasından doldurulacak)
            if (rec.SelectedChannels.Count > 0 || rec.SelectedExtraSources.Count > 0)
            {
                try
                {
                    var profile = await _customers.GetProfileAsync(customer.TenantKey, ct);
                    if (profile is not null)
                    {
                        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase);
                        var allCodes = rec.SelectedChannels.Concat(rec.SelectedExtraSources);
                        foreach (var code in allCodes)
                        {
                            var descriptor = OrderChannelCatalog.FindByCode(code);
                            if (descriptor is null) continue;
                            // Sadece enabled flag'ini ayarla; credential alanları boş kalır
                            OrderChannelMetadata.Apply(metadata, descriptor.Type,
                                enabled: true, defaultProvider: null,
                                fieldValues: new Dictionary<string, string>());
                        }

                        await _customers.UpsertAsync(customer.TenantKey, new UpsertCustomerRequest
                        {
                            TenantKey = customer.TenantKey,
                            Name = profile.Name,
                            IsActive = profile.IsActive,
                            AllowedProviders = profile.AllowedProviders.ToList(),
                            NotificationTargets = profile.NotificationTargets.ToList(),
                            Metadata = metadata
                        }, ct);
                    }
                }
                catch (Exception metaEx)
                {
                    _logger.LogWarning(metaEx, "Selected channels metadata write failed for tenant {Tenant}", customer.TenantKey);
                    // kritik degil — kullanici sonra elle aktive eder
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnsureCustomer failed for {Email}", rec.Email);
            ViewData["Title"] = "Hesap Olusturulamadi";
            ViewData["Reason"] = "Hesabiniz olusturulurken bir hata olustu. Destek ile iletisime gecin.";
            return View("ConfirmError");
        }

        ViewData["Title"] = "Hesap Hazir";
        ViewData["TenantCode"] = tenantCode;
        ViewData["Email"] = rec.Email;
        return View("ConfirmDone");
    }

    private static string WebUtilEncode(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string SanitizeCode(string raw)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in raw.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') sb.Append(ch);
            else if (ch == '.' || ch == ' ') sb.Append('-');
        }
        var code = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(code) ? $"tenant-{Guid.NewGuid():N}".Substring(0, 12) : code;
    }
}
