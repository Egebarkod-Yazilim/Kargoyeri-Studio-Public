using System.Globalization;
using Kargoyeri.Infrastructure.DependencyInjection;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Infrastructure.EInvoice;
using Kargoyeri.Studio.Core.Infrastructure.Payments;
using Kargoyeri.Studio.Core.Infrastructure.Storage;
using Kargoyeri.Studio.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kargoyeri.Studio.Core;

public static class StudioServiceExtensions
{
    /// <summary>
    /// Kargoyeri Studio icin gerekli tum servisleri DI'a kaydeder.
    /// Hem standalone (Web) hem de gomulu (Embedded) host tarafindan cagrilir.
    /// </summary>
    public static IServiceCollection AddKargoyeriStudio(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        ValidateConfiguration(configuration);

        services.AddKargoyeriCore(configuration, contentRootPath);
        services.AddHttpContextAccessor();

        // P2-#7 — Multi-language (TR/EN). IStringLocalizer SharedResource.resx (tr-TR base) +
        // SharedResource.en.resx (English) ile beslenir. Kullanici secimi
        // CookieRequestCultureProvider cookie'sinde tutulur (CultureController.Set yazar).
        services.AddLocalization(o => o.ResourcesPath = "Resources");
        services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(o =>
        {
            var supported = new[]
            {
                new CultureInfo("tr-TR"),
                new CultureInfo("en-US")
            };
            o.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("tr-TR");
            o.SupportedCultures = supported;
            o.SupportedUICultures = supported;
        });

        // Granular authorization — tenant rollerini policy olarak yayinla
        services.AddAuthorization(options =>
        {
            options.AddPolicy(StudioRoles.CanWrite, p =>
                p.RequireAssertion(ctx =>
                {
                    if (ctx.User.IsInRole(StudioRoles.Admin)) return true;
                    if (!ctx.User.IsInRole(StudioRoles.Operator)) return false;
                    var r = ctx.User.FindFirst(StudioRoles.TenantRoleClaim)?.Value ?? TenantUserRoles.Operator;
                    return r == TenantUserRoles.Operator || r == TenantUserRoles.Manager;
                }));

            options.AddPolicy(StudioRoles.CanManageTenant, p =>
                p.RequireAssertion(ctx =>
                    ctx.User.IsInRole(StudioRoles.Admin) ||
                    (ctx.User.IsInRole(StudioRoles.Operator) &&
                     ctx.User.FindFirst(StudioRoles.TenantRoleClaim)?.Value == TenantUserRoles.Manager)));
        });
        services.AddScoped<IStudioWorkspaceContext, StudioWorkspaceContext>();
        services.AddSingleton<IStudioEmailSender, StudioEmailSender>();
        services.AddScoped<StudioProviderStatusService>();
        services.AddSingleton<WorkerStatusTracker>();
        services.AddSingleton<IStudioActivityLogService, PersistentStudioActivityLogService>();
        services.AddSingleton<CsvImportJobService>();
        services.AddSingleton<TrackingNumberIndex>();
        services.AddSingleton<ProviderHealthMonitor>();
        services.AddSingleton<OutboundWebhookRetryQueue>();
        services.AddSingleton<ShipmentArchiveIndex>();
        services.AddScoped<TenantUserService>();
        services.AddScoped<PickupRequestService>();
        services.AddScoped<NotificationTemplateStore>();
        services.AddScoped<DashboardPreferenceStore>();
        services.AddScoped<WorkspaceBrandingStore>();
        services.AddScoped<WorkflowRuleStore>();
        services.AddSingleton<WorkflowExecutionStore>();
        services.AddSingleton<PendingSignupStore>();
        services.AddScoped<ProviderCertificationRunner>();

        // P5-#11 — Multi-source order channels (Marketplace API'leri, NopCommerce, Embedded mini-shop).
        // Manuel sipariş girişi her zaman aktif; bu kanallar onun yanına eklenir.
        services.AddSingleton<Infrastructure.OrderChannels.OrderChannelSyncMonitor>();
        // Canli (Live) adapter'lar — gercek pazaryeri API'leri. Registry First() ile cozdugu icin
        // Simulation versiyonlarindan once kayitli olmali.
        services.AddHttpClient("hepsiburada-orders");
        services.AddHttpClient("trendyol-orders");
        services.AddHttpClient("n11-orders");
        services.AddHttpClient("pazarama-orders");
        services.AddHttpClient("ciceksepeti-orders");
        services.AddHttpClient("amazon-orders");
        services.AddHttpClient("amazon-lwa");
        services.AddHttpClient("pttavm-orders");
        services.AddHttpClient("modanisa-orders");
        // E-Ticaret platformlari HTTP clients
        services.AddHttpClient("shopify-orders");
        services.AddHttpClient("woocommerce-orders");
        services.AddHttpClient("ideasoft-orders");
        services.AddHttpClient("ideasoft-oauth");
        services.AddHttpClient("ticimax-orders");
        services.AddHttpClient("nopcommerce-orders");
        services.AddHttpClient("nopcommerce-auth");
        // Live adapter'lar — Registry .First() kullandigi icin Simulation'dan ONCE kayit
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.HepsiburadaLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.TrendyolLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.N11LiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.PazaramaLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.CicekSepetiLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.AmazonLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.PttAvmLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.ModanisaLiveAdapter>();
        // E-Ticaret platformlari Live adapter'lari
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.ShopifyLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.WooCommerceLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.IdeaSoftLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.TicimaxLiveAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.NopCommerceLiveAdapter>();
        // Simulation fallback'lar — Live'in cagrildigi senaryolarda devreye girmez
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.CicekSepetiOrderChannelAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.GittiGidiyorOrderChannelAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.PazaramaOrderChannelAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.AmazonOrderChannelAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.PttAvmOrderChannelAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.ModanisaOrderChannelAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.NopCommerceOrderChannelAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.IOrderChannelAdapter, Infrastructure.OrderChannels.Adapters.EmbeddedShopOrderChannelAdapter>();
        services.AddSingleton<Infrastructure.OrderChannels.OrderChannelRegistry>();
        services.AddScoped<Infrastructure.OrderChannels.OrderChannelSyncEngine>();

        // P5-#8 — Pluggable blob storage. Default: local FS.
        // Studio:Storage:Provider = "Local" | "S3" | "Azure"
        var storageProvider = configuration["Studio:Storage:Provider"] ?? "Local";
        services.AddSingleton<LocalFileBlobStorage>();
        switch (storageProvider.ToLowerInvariant())
        {
            case "s3":
                var s3Opt = new S3Options
                {
                    Endpoint    = configuration["Studio:Storage:S3:Endpoint"]  ?? "",
                    Region      = configuration["Studio:Storage:S3:Region"]    ?? "us-east-1",
                    AccessKey   = configuration["Studio:Storage:S3:AccessKey"] ?? "",
                    SecretKey   = configuration["Studio:Storage:S3:SecretKey"] ?? "",
                    Bucket      = configuration["Studio:Storage:S3:Bucket"]    ?? "",
                    UsePathStyle = bool.TryParse(configuration["Studio:Storage:S3:UsePathStyle"], out var ps) && ps
                };
                services.AddSingleton(s3Opt);
                services.AddHttpClient("studio-s3");
                services.AddSingleton<IBlobStorage, S3BlobStorage>();
                break;
            case "azure":
                services.AddSingleton<IBlobStorage, AzureBlobStorage>();
                break;
            default:
                services.AddSingleton<IBlobStorage>(sp => sp.GetRequiredService<LocalFileBlobStorage>());
                break;
        }
        services.AddScoped<WorkflowNotificationDispatcher>();
        services.AddScoped<AuditExportPackageService>();
        services.AddSingleton<MarketplaceSyncMonitor>();
        services.AddScoped<ShipmentAddressValidationService>();
        services.AddScoped<ShipmentAnomalyDetectionService>();
        services.AddScoped<MarketplaceOrderSyncEngine>();
        services.AddHttpClient("studio-marketplace");
        services.AddHttpClient("studio-address-validation");
        services.AddMemoryCache();
        services.AddScoped<TrackingLookupService>();
        services.AddScoped<StudioActivityLogFilter>();
        services.AddScoped<LicenseGuardFilter>();
        services.Configure<MvcOptions>(o =>
        {
            o.Filters.AddService<LicenseGuardFilter>();      // oturum icindeki lisans kontrolu
            o.Filters.AddService<StudioActivityLogFilter>();
        });
        // Gerçek Infrastructure katmanı (decompile edilmiş kaynak) devrede olduğundan
        // arka plan (hosted) servisleri her zaman kaydedilir. Eski "recovered/stub"
        // in-memory modu kaldırıldı.
        var recoveredRuntimeMode = false;
        if (!recoveredRuntimeMode)
        {
            services.AddHostedService<StudioShipmentRefreshService>();
            services.AddHostedService<TrackingNumberIndexRefreshService>();
            services.AddHostedService<ProviderHealthProbeService>();
            services.AddHostedService<DataRetentionService>();
            services.AddHostedService<OutboundWebhookRetryService>();
            services.AddHostedService<WorkflowAutomationService>();
            services.AddHostedService<MarketplaceOrderSyncService>();
        }

        // P4-#1 — Tenant metrik rollup
        services.AddScoped<TenantMetricsAggregator>();
        if (!recoveredRuntimeMode)
        {
            services.AddHostedService<TenantMetricsHostedService>();
        }

        // P4-#2 — Billing
        services.AddScoped<BillingService>();

        // P5-#3 — Payment gateway (Iyzico, Noop fallback)
        services.AddHttpClient("studio-iyzico");
        var paymentProvider = (configuration["Studio:Payments:Provider"] ?? "noop").ToLowerInvariant();
        switch (paymentProvider)
        {
            case "iyzico": services.AddScoped<IPaymentGateway, IyzicoPaymentGateway>(); break;
            default:       services.AddScoped<IPaymentGateway, NoopPaymentGateway>(); break;
        }

        // P5-#3 — e-Arsiv gateway (Korgun, Uyumsoft, GIB, Noop fallback)
        services.AddHttpClient("studio-korgun");
        services.AddHttpClient("studio-uyumsoft");
        var earchiveProvider = (configuration["Studio:EArchive:Provider"] ?? "noop").ToLowerInvariant();
        switch (earchiveProvider)
        {
            case "korgun":   services.AddScoped<IEArchiveGateway, KorgunEArchiveGateway>(); break;
            case "uyumsoft": services.AddScoped<IEArchiveGateway, UyumsoftEArchiveGateway>(); break;
            case "gib":      services.AddScoped<IEArchiveGateway, GibPortalEArchiveGateway>(); break;
            default:         services.AddScoped<IEArchiveGateway, NoopEArchiveGateway>(); break;
        }

        services.AddHealthChecks()
            .AddCheck("studio-core", () => HealthCheckResult.Healthy("Studio core servisleri calisiyor."))
            .AddCheck<StudioStorageHealthCheck>("studio-storage", tags: new[] { "ready", "storage" })
            .AddCheck<StudioTenantRepositoryHealthCheck>("studio-tenant-repo", tags: new[] { "ready", "db" });

        // REST API korumasi icin ApiKey secenegi (opsiyonel, bos ise atlanir)
        services.AddOptions<StudioApiKeyOptions>()
            .BindConfiguration("StudioApi");

        return services;
    }

    private static void ValidateConfiguration(IConfiguration configuration)
    {
        var errors = new List<string>();

        var storagePath = configuration["Kargoyeri:Storage:BasePath"] ?? configuration["Storage:BasePath"];
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            // Zorunlu degil — varsayilan yol kullanilir, uyari gosterilir
        }

        var refreshInterval = configuration["Kargoyeri:Processing:RefreshIntervalSeconds"];
        if (refreshInterval is not null && !int.TryParse(refreshInterval, out _))
        {
            errors.Add("Kargoyeri:Processing:RefreshIntervalSeconds gecerli bir sayi olmali.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Kargoyeri Studio yapilandirma hatasi:{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors.Select(e => $"  - {e}")));
        }
    }
}
