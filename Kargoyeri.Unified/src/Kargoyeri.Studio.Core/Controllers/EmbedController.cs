using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P4-#3 — Iframe-friendly public tracker.
///
/// Kullanim (musteri sitesinde):
/// <code>
/// &lt;iframe src="https://studio.kargoyeri.com/embed/track/ABC123XYZ"
///         width="100%" height="520" style="border:0;border-radius:12px;"&gt;&lt;/iframe&gt;
/// </code>
///
/// X-Frame-Options bu controller'in cevaplarinda kaldirilir; CSP frame-ancestors *
/// olarak ayarlanir (musteri kendi domain'inde embed edebilsin).
/// </summary>
[AllowAnonymous]
[Route("embed")]
public sealed class EmbedController : Controller
{
    private readonly TrackingLookupService _lookup;

    public EmbedController(TrackingLookupService lookup)
    {
        _lookup = lookup;
    }

    [HttpGet("track")]
    public IActionResult Form()
    {
        ApplyEmbedHeaders();
        return View("Form");
    }

    [HttpGet("track/{trackingNumber}")]
    public async Task<IActionResult> Detail(string trackingNumber, CancellationToken ct)
    {
        ApplyEmbedHeaders();

        var vm = new PublicTrackingResultViewModel
        {
            TrackingNumber = (trackingNumber ?? string.Empty).Trim()
        };

        if (string.IsNullOrWhiteSpace(vm.TrackingNumber))
        {
            return View("Form");
        }

        var result = await _lookup.FindAsync(vm.TrackingNumber, ct);
        if (result is null)
        {
            vm.Found = false;
            vm.Message = "Bu takip numarasi bulunamadi.";
            return View("Detail", vm);
        }

        vm.Found = true;
        var branding = WorkspaceFeatureMetadata.ReadBranding(result.Tenant);
        vm.BrandName = !string.IsNullOrWhiteSpace(branding.DisplayName)
            ? branding.DisplayName
            : string.IsNullOrWhiteSpace(result.Tenant.Name) ? result.Tenant.TenantKey : result.Tenant.Name;
        vm.BrandLogoUrl = branding.LogoUrl;

        ViewData["BrandName"] = vm.BrandName;
        ViewData["BrandLogoUrl"] = vm.BrandLogoUrl;
        ViewData["BrandAccentColor"] = branding.AccentColor;

        var s = result.Shipment;
        vm.Provider = s.Provider;
        vm.Status = s.Status;
        vm.StatusText = s.Status.ToString();
        vm.CreatedAtUtc = s.CreatedAtUtc;
        vm.LastUpdatedAtUtc = s.UpdatedAtUtc;

        return View("Detail", vm);
    }

    private void ApplyEmbedHeaders()
    {
        // Iframe'de calisabilmesi icin frame-restrict header'larini kaldir.
        Response.Headers.Remove("X-Frame-Options");
        Response.Headers["Content-Security-Policy"] = "frame-ancestors *";
        Response.Headers["Cache-Control"] = "public, max-age=30";
    }
}
