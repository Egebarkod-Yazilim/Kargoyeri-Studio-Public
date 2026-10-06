using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Her istek icin bir correlation ID uretir (veya gelen X-Correlation-ID header'ini alir),
/// loglarin bir request'i takip etmesi icin ILogger scope'una ve response header'ina ekler.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName  = "X-Correlation-ID";
    private const string ItemName    = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N")[..16];

        context.Items[ItemName]              = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        var user     = context.User?.Identity?.IsAuthenticated == true ? context.User.Identity.Name : "-";
        var tenant   = context.User?.FindFirst(StudioRoles.WorkspaceCodeClaim)?.Value ?? "-";

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["User"]          = user!,
            ["Tenant"]        = tenant,
            ["Path"]          = context.Request.Path.Value ?? string.Empty,
            ["Method"]        = context.Request.Method
        }))
        {
            await _next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();

    public static string? GetCorrelationId(this HttpContext context) =>
        context.Items["CorrelationId"] as string;
}
