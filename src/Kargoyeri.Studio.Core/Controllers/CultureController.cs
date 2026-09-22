using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P2-#7 — Kullanici dil tercihini cookie'ye yazar (CookieRequestCultureProvider).
/// Desteklenen kulturler: tr (varsayilan), en. UseRequestLocalization middleware'i bu cookie'yi okur.
/// </summary>
[AllowAnonymous]
[Route("culture")]
public class CultureController : Controller
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase) { "tr", "en" };

    [HttpGet("set")]
    public IActionResult Set(string culture, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(culture) || !Supported.Contains(culture))
        {
            culture = "tr";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }
}
