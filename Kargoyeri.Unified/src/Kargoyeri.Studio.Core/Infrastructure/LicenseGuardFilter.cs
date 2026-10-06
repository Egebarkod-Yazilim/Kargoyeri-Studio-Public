using Kargoyeri.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Her request'te musteri oturumunun lisansini kontrol eder. Suresi dolmussa
/// kullaniciyi logout edip /Access/Login'e "Lisansiniz doldu" mesajiyla yonlendirir.
/// Admin'i ve Access controller'i atlar.
/// </summary>
public sealed class LicenseGuardFilter : IAsyncActionFilter
{
    private readonly CustomerService _customerService;

    public LicenseGuardFilter(CustomerService customerService)
    {
        _customerService = customerService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var user = http.User;

        // Admin kontrolsuz gecer, anonim (Login sayfasi) da gecer
        if (user?.Identity?.IsAuthenticated != true
            || user.IsInRole(StudioRoles.Admin))
        {
            await next();
            return;
        }

        var controller = (context.RouteData.Values["controller"] as string) ?? string.Empty;
        if (controller.Equals("Access", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var tenantKey = user.FindFirst(StudioRoles.WorkspaceCodeClaim)?.Value;
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            await next();
            return;
        }

        var profile = await _customerService.GetProfileAsync(tenantKey, http.RequestAborted);
        var license = LicenseHelper.Read(profile?.Metadata);

        if (license.HasLicense && license.IsExpired)
        {
            // Oturumu kapat ve login sayfasina yonlendir
            await http.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);

            var tempDataFactory = http.RequestServices.GetRequiredService<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>();
            var tempData        = tempDataFactory.GetTempData(http);
            StudioFlash.Error(tempData, "Lisansinizin suresi doldu. Lutfen yetkili ile iletisime gecin.");
            tempData.Save();

            context.Result = new RedirectToActionResult("Login", "Access", null);
            return;
        }

        await next();
    }
}
