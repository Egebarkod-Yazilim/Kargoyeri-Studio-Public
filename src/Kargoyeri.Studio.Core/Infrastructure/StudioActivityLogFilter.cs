using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Her MVC aksiyonunu otomatik olarak aktivite loguna kaydeder.
/// Login/Logout ve statik dosya istekleri hariç tutulur.
/// </summary>
public sealed class StudioActivityLogFilter : IAsyncActionFilter
{
    // Log dışı bırakılacak controller'lar
    private static readonly HashSet<string> SkipControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Access"  // Login/Logout kendi başına loglanmıyor
    };

    private readonly IStudioActivityLogService _logService;

    public StudioActivityLogFilter(IStudioActivityLogService logService)
    {
        _logService = logService;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();

        try
        {
            var routeData   = context.RouteData;
            var controller  = routeData.Values["controller"]?.ToString() ?? "";
            var action      = routeData.Values["action"]?.ToString() ?? "";

            if (SkipControllers.Contains(controller))
                return;

            var user        = context.HttpContext.User;
            var tenantKey   = user.FindFirstValue(StudioRoles.WorkspaceCodeClaim) ?? "admin";
            var username    = user.FindFirstValue(StudioRoles.UsernameClaim)
                           ?? user.FindFirstValue(ClaimTypes.Name)
                           ?? "unknown";
            var role        = user.IsInRole(StudioRoles.Admin) ? "Admin" : "Operator";
            var method      = context.HttpContext.Request.Method;
            var path        = context.HttpContext.Request.Path.Value ?? "/";
            var ip          = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var statusCode  = context.HttpContext.Response.StatusCode;

            _logService.Append(new StudioActivityLogEntry(
                Id:            Guid.NewGuid(),
                TenantKey:     tenantKey,
                Username:      username,
                Role:          role,
                Controller:    controller,
                Action:        action,
                HttpMethod:    method,
                Path:          path,
                IpAddress:     ip,
                StatusCode:    statusCode,
                OccurredAtUtc: DateTimeOffset.UtcNow));
        }
        catch
        {
            // Log yazma hatası uygulamayı durdurmamalı
        }
    }
}
