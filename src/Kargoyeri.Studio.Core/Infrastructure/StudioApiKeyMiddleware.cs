using Kargoyeri.Application.Services;
using Microsoft.Extensions.Options;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// /api/* rotalarini API key ile korur.
/// Header: X-Api-Key: {key}
///
/// Auth kaynaklari (sirayla):
///   1) Global key — appsettings StudioApi:ApiKey (tum tenant'lar icin gecerli)
///   2) Tenant-bound key — TenantApiKeyStore (tenant.Metadata["api.keys"]).
///      Bu durumda HttpContext.Items["studio:api-tenantKey"] tenant'a yazilir,
///      controller'lar bu degeri okuyabilir.
///
/// Her iki anahtar da bos/tanimsizsa (development) gecirir.
/// </summary>
public sealed class StudioApiKeyMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    public  const string TenantContextKey = "studio:api-tenantKey";
    public  const string KeyIdContextKey  = "studio:api-keyId";

    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<StudioApiKeyOptions> _options;
    private readonly ILogger<StudioApiKeyMiddleware> _logger;

    public StudioApiKeyMiddleware(
        RequestDelegate next,
        IOptionsMonitor<StudioApiKeyOptions> options,
        ILogger<StudioApiKeyMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, CustomerService customerService)
    {
        // Sadece /api/ ile baslayan rotalari kontrol et
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Webhook callbacks kendi imza dogrulamasi yapar — middleware atlanir
        if (context.Request.Path.StartsWithSegments("/api/webhook", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var configuredKey = _options.CurrentValue.ApiKey;

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var providedRaw)
            || string.IsNullOrWhiteSpace(providedRaw))
        {
            // Global key tanimli degilse (dev) anonim gecirir
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                await _next(context);
                return;
            }
            await Reject(context, "X-Api-Key header'i gerekli.");
            return;
        }

        var provided = providedRaw.ToString();

        // 1) Global key eslesir mi?
        if (!string.IsNullOrWhiteSpace(configuredKey)
            && string.Equals(provided, configuredKey, StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        // 2) Tenant-bound API key ara — tum tenant'lari tara, hash karsilastirma yap
        var tenants = await customerService.ListAllAsync(context.RequestAborted);
        foreach (var tenant in tenants)
        {
            var match = TenantApiKeyStore.TryMatch(tenant.Metadata, provided);
            if (match is null) continue;

            context.Items[TenantContextKey] = tenant.TenantKey;
            context.Items[KeyIdContextKey]  = match.Id;

            // LastUsedAt'i async guncelle (best-effort, hatayi yutar)
            try
            {
                var meta = TenantApiKeyStore.TouchLastUsed(tenant.Metadata, match.Id);
                await customerService.UpsertAsync(tenant.TenantKey, new Kargoyeri.Contracts.Dtos.UpsertCustomerRequest
                {
                    TenantKey           = tenant.TenantKey,
                    Name                = tenant.Name,
                    IsActive            = tenant.IsActive,
                    AllowedProviders    = tenant.AllowedProviders.ToList(),
                    NotificationTargets = tenant.NotificationTargets.ToList(),
                    Metadata            = meta
                }, context.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "API key LastUsedAt guncellenemedi (gecirildi).");
            }

            await _next(context);
            return;
        }

        // Global tanimli ama eslesmedi VE tenant key de bulunmadi
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            // Hicbir global yok ama gelen key de eslesmedi — yine de gecirelim mi?
            // Hayir: bilinmeyen key gonderiliyor, reddet.
            await Reject(context, "Gecersiz API key.");
            return;
        }

        await Reject(context, "Gecersiz veya bilinmeyen API key.");
    }

    private async Task Reject(HttpContext context, string message)
    {
        _logger.LogWarning("API key dogrulama basarisiz. Path: {Path}, IP: {IP}",
            context.Request.Path, context.Connection.RemoteIpAddress);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            $$"""{"error":"{{message}}"}""");
    }
}

/// <summary>
/// Middleware kayit extension metodu.
/// </summary>
public static class StudioApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseStudioApiKey(this IApplicationBuilder app)
        => app.UseMiddleware<StudioApiKeyMiddleware>();
}
