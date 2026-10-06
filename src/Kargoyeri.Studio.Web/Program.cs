using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core;
using Kargoyeri.Studio.Core.Infrastructure;
// CorrelationIdMiddlewareExtensions lives in Kargoyeri.Studio.Core.Infrastructure
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using System.Reflection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys", "studio-web");
Directory.CreateDirectory(dataProtectionPath);

// SERILOG — yapilandirilmis log. Konfig "Serilog" section'indan okunur.
// Seq URL tanimliysa Seq'e gonderir; yoksa Console + rolling file kullanilir.
// CorrelationId / User / Tenant scope'lari Serilog'un FromLogContext enricher'i ile satirlara islenir.
var seqUrl = builder.Configuration["Serilog:Seq:ServerUrl"] ?? builder.Configuration["Seq:ServerUrl"];
var logsPath = Path.Combine(builder.Environment.ContentRootPath, "logs", "studio-.log");
Directory.CreateDirectory(Path.GetDirectoryName(logsPath)!);

var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "Kargoyeri.Studio")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(logsPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}");

if (!string.IsNullOrWhiteSpace(seqUrl))
{
    var seqApiKey = builder.Configuration["Serilog:Seq:ApiKey"] ?? builder.Configuration["Seq:ApiKey"];
    loggerConfig = loggerConfig.WriteTo.Seq(seqUrl, apiKey: string.IsNullOrWhiteSpace(seqApiKey) ? null : seqApiKey);
}

Log.Logger = loggerConfig.CreateLogger();
Log.Information("Kargoyeri.Studio basliyor. Environment={Env} SeqEnabled={SeqEnabled}",
    builder.Environment.EnvironmentName, !string.IsNullOrWhiteSpace(seqUrl));

builder.Host.UseSerilog();

// =============================================================================
// P5-#5 — Sentry: hata raporlama + performans tracing.
// DSN konfigden gelir; bos ise Sentry passthrough mode'da calisir (event yazmaz).
// =============================================================================
var sentryDsn = builder.Configuration["Studio:Sentry:Dsn"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.Environment = builder.Environment.EnvironmentName;
        o.Release = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        // Production'da %10 trace; dev'de %100.
        o.TracesSampleRate = builder.Environment.IsDevelopment() ? 1.0 : 0.1;
        o.SendDefaultPii = false;
        o.AttachStacktrace = true;
        o.MaxBreadcrumbs = 50;
        // 4xx HTTP'leri Sentry'e gondermeyelim — 5xx'ler kafidir.
        o.MinimumEventLevel = LogLevel.Error;
        o.MinimumBreadcrumbLevel = LogLevel.Information;
    });
    Log.Information("Sentry enabled. Environment={Env}", builder.Environment.EnvironmentName);
}
else
{
    Log.Information("Sentry disabled (Studio:Sentry:Dsn not configured).");
}

// =============================================================================
// P5-#5 — OpenTelemetry: distributed tracing + metrics.
// OTLP endpoint konfigden gelir (Studio:Otel:Endpoint), ornek: http://otel-collector:4317
// Bos ise OTel passthrough — her sey local diagnostic listener uzerinde kalir.
// =============================================================================
var otelEndpoint = builder.Configuration["Studio:Otel:Endpoint"];
var otelServiceName = builder.Configuration["Studio:Otel:ServiceName"] ?? "kargoyeri-studio";
if (!string.IsNullOrWhiteSpace(otelEndpoint))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r
            .AddService(serviceName: otelServiceName,
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                        serviceInstanceId: Environment.MachineName)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = builder.Environment.EnvironmentName,
                ["studio.region"]           = builder.Configuration["Studio:Region"] ?? "tr-central"
            }))
        .WithTracing(t => t
            .AddAspNetCoreInstrumentation(o =>
            {
                // Health check + static asset spam'ini filtrele
                o.Filter = ctx =>
                    !ctx.Request.Path.StartsWithSegments("/healthz") &&
                    !ctx.Request.Path.StartsWithSegments("/_content") &&
                    !ctx.Request.Path.StartsWithSegments("/favicon");
                o.RecordException = true;
            })
            .AddHttpClientInstrumentation(o =>
            {
                o.RecordException = true;
            })
            .AddSqlClientInstrumentation(o =>
            {
                o.SetDbStatementForText = false; // PII / data leakage riskine karsi
                o.RecordException = true;
            })
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint)))
        .WithMetrics(m => m
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint)));
    Log.Information("OpenTelemetry enabled. Endpoint={Endpoint} Service={Service}", otelEndpoint, otelServiceName);
}
else
{
    Log.Information("OpenTelemetry disabled (Studio:Otel:Endpoint not configured).");
}

builder.Services.Configure<StudioAccessOptions>(builder.Configuration.GetSection("StudioAccess"));

// Reverse proxy (Nginx/IIS) arkasinda calistiginda gercek client IP ve scheme'i tanir.
// Production'da rate limiting'in dogru calismasi ve UseHttpsRedirection'in scheme'i yanlis okumamasi icin kritik.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust local + private network — production'da spesifik proxy IP'sini KnownProxies'a eklemek tercih edilir
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// PRODUCTION GUARD — at least one configured SuperAdmin account is required.
if (!builder.Environment.IsDevelopment())
{
    var accessOptions = builder.Configuration.GetSection("StudioAccess").Get<StudioAccessOptions>() ?? new();
    var hasSuperAdmin = accessOptions.Admins.Any(a => a.IsActive
                       && string.Equals(a.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                       && !string.IsNullOrWhiteSpace(a.Email)
                       && (!string.IsNullOrWhiteSpace(a.Password) || !string.IsNullOrWhiteSpace(a.PasswordHash)));

    if (!hasSuperAdmin)
    {
        throw new InvalidOperationException(
            "PRODUCTION GUARD: StudioAccess:Admins listesinde e-posta, parola ve Role=SuperAdmin tanimli aktif bir hesap gereklidir.");
    }
    foreach (var a in accessOptions.Admins.Where(x => x.IsActive))
    {
        if (!string.IsNullOrWhiteSpace(a.Password) && a.Password.Length < 8)
            throw new InvalidOperationException(
                $"PRODUCTION GUARD: Admin '{a.Email}' parolasi en az 8 karakter olmalidir " +
                "(daha iyisi: PasswordHash alaninda BCrypt hash kullanin).");
    }
}

// Core (RCL) assembly'sindeki controller ve view'ları kesfet
var coreAssembly = typeof(StudioServiceExtensions).Assembly;
builder.Services
    .AddControllersWithViews()
    .AddApplicationPart(coreAssembly);
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Access/Login";
        options.AccessDeniedPath = "/Access/Login";
        options.Cookie.Name = "kargoyeri-studio-auth";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        // COOKIE SERTLESTIRME
        options.Cookie.HttpOnly = true;
        // Local Development profile may run over HTTP; production cookies stay HTTPS-only.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
            : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax; // OAuth callback icin Lax
        options.Cookie.IsEssential = true;
    });

// Antiforgery cookie'si de sertlesir
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    // Antiforgery enforces SSL for Always and throws while rendering forms over local HTTP.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
        : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
});

// HSTS — production'da tarayici HTTPS'i 1 yil cache'ler
builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var dataProtection = builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("Kargoyeri.Studio");

if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}

// Rate limiting — brute-force korumasi. IP basina login denemesi: 10 / 5 dakika.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
            await context.HttpContext.Response.WriteAsync(
                "<h2>Cok fazla deneme</h2><p>Lutfen 1-5 dakika sonra tekrar deneyin.</p>", ct);
        }
    };

    options.AddPolicy("login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit         = 10,
            Window              = TimeSpan.FromMinutes(5),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit          = 0
        });
    });

    // P5-#4 — Self-service signup brute-force korumasi. IP basina 5 / 10 dakika.
    options.AddPolicy("signup", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit         = 5,
            Window              = TimeSpan.FromMinutes(10),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit          = 0
        });
    });

    // P2-#1 — Per-tenant API rate limit. Tenant key partition (StudioApiKeyMiddleware
    // tarafindan HttpContext.Items["studio:api-tenantKey"] yazilir). Anonim cagrilar
    // veya tenant'siz global key icin "default" partition'a duser.
    options.AddPolicy("api-tenant", httpContext =>
    {
        var tenantKey = httpContext.Items[StudioApiKeyMiddleware.TenantContextKey] as string
                        ?? "global";
        return RateLimitPartition.GetTokenBucketLimiter(tenantKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit          = 1000,
            TokensPerPeriod     = 1000,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit          = 0,
            AutoReplenishment   = true
        });
    });
});

builder.Services.AddKargoyeriStudio(builder.Configuration, builder.Environment.ContentRootPath);

// SWAGGER / OpenAPI — /api/v1/* endpoint'leri icin entegrasyon dokumantasyonu.
// Swagger UI: /swagger    OpenAPI JSON: /swagger/v1/swagger.json
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Kargoyeri API",
        Version     = "v1",
        Description = "Kargoyeri dis sistem entegrasyon REST API'si. " +
                      "Tum istekler X-Api-Key header'i ile dogrulanir (yapilandirildiysa).",
        Contact     = new OpenApiContact { Name = "Kargoyeri Destek", Email = "destek@kargoyeri.com" }
    });

    // X-Api-Key header'i ile auth
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In   = ParameterLocation.Header,
        Name = "X-Api-Key",
        Description = "Tenant'a verilen API anahtari. StudioApi:Keys yapilandirmasinda tanimli olmali."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
            },
            Array.Empty<string>()
        }
    });

    // Sadece /api/* rotalarini Swagger'a dahil et — MVC sayfalarini gosterme
    options.DocInclusionPredicate((_, apiDesc) =>
    {
        var route = apiDesc.RelativePath ?? string.Empty;
        return route.StartsWith("api/", StringComparison.OrdinalIgnoreCase);
    });

    // Web ve Core projelerinin XML dokumantasyon dosyalarini dahil et (varsa)
    var baseDir = AppContext.BaseDirectory;
    foreach (var asm in new[] { Assembly.GetExecutingAssembly(), typeof(StudioServiceExtensions).Assembly })
    {
        var xmlFile = Path.Combine(baseDir, $"{asm.GetName().Name}.xml");
        if (File.Exists(xmlFile))
        {
            options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
        }
    }
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    // SQL Server modu aktifse DbContext varsa semayi olustur (ilk calistirmada tablolari kurar).
    var dbContextType = Type.GetType(
        "Kargoyeri.Infrastructure.Persistence.SqlServer.KargoyeriDbContext, Kargoyeri.Infrastructure");
    var db = dbContextType is null
        ? null
        : scope.ServiceProvider.GetService(dbContextType) as DbContext;
    if (db is not null)
    {
        var appLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        try
        {
            // Migration dosyalarini uygular (InitialCreate + sonrasi).
            // Ilk defa calisiyorsa semayi olusturur, sonraki calistirmalar pending migration'lari uygular.
            await db.Database.MigrateAsync();
            appLogger.LogInformation("SQL Server semasi hazir (migrations uygulandi).");
        }
        catch (Exception ex)
        {
            appLogger.LogError(ex, "SQL Server baglantisi / migration basarisiz. Baglanti dizesini kontrol edin.");
            throw;
        }
    }

    var bootstrapper = scope.ServiceProvider.GetService<CustomerBootstrapper>();
    if (bootstrapper is not null)
    {
        var startupLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        try
        {
            await bootstrapper.EnsureSeededAsync(CancellationToken.None);
        }
        catch (NotImplementedException)
        {
            startupLogger.LogWarning("Customer bootstrapper source implementation is not recovered yet; seed step skipped.");
        }
    }
}

// Reverse proxy header'larini ilk middleware olarak isle — UseHttpsRedirection'dan once olmali
// ki proxy HTTPS terminate ettiyse scheme dogru okunsun.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// SECURITY HEADERS — XSS/Clickjacking/Sniffing koruma
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"]  = "nosniff";
    h["X-Frame-Options"]          = "DENY";
    h["Referrer-Policy"]          = "strict-origin-when-cross-origin";
    h["Permissions-Policy"]       = "geolocation=(), microphone=(), camera=()";
    // CSP: inline stil/script kullanimi var (asp.net tag helper'lar); gerekirse nonce ile sikilastirilabilir
    // QR kod servisi (api.qrserver.com) icin img-src'a izin verilir
    h["Content-Security-Policy"]  =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https://api.qrserver.com; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

// SWAGGER UI — /swagger altinda OpenAPI dokumani ve interaktif test arabirimi.
// Production'da da acik birakildi: B2B entegrasyon dokumani; X-Api-Key zaten korur.
app.UseSwagger(c => c.RouteTemplate = "swagger/{documentName}/swagger.json");
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Kargoyeri API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Kargoyeri API";
});

app.UseStaticFiles();

// P2-#7 — Request localization (TR/EN). Cookie tabanli kultur secimi;
// SharedResource.resx (tr) / SharedResource.en.resx ile IStringLocalizer'i besler.
var locOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(locOptions);

app.UseCorrelationId(); // her istege unique trace ID + log scope
// Her HTTP istegi icin tek satirlik ozet log (method, path, status, elapsed). 4xx/5xx daha yuksek seviyede loglar.
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} -> {StatusCode} in {Elapsed:0} ms";
    opts.GetLevel = (httpCtx, elapsed, ex) =>
        ex != null || httpCtx.Response.StatusCode >= 500 ? LogEventLevel.Error
        : httpCtx.Response.StatusCode >= 400 ? LogEventLevel.Warning
        : LogEventLevel.Information;
});
app.UseRouting();
app.UseStudioApiKey(); // /api/* rotalarini X-Api-Key ile korur
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// [ApiController] attribute route'lari icin (ShipmentsApiController, ProvidersApiController)
app.MapControllers();

// MVC convention routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Detayli saglik durumu — her check adi, sure, mesaj ve data dondurur
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            status      = report.Status.ToString(),
            totalMs     = report.TotalDuration.TotalMilliseconds,
            checkedAtUtc = DateTimeOffset.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs  = e.Value.Duration.TotalMilliseconds,
                tags        = e.Value.Tags,
                data        = e.Value.Data,
                error       = e.Value.Exception?.Message
            })
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
});

// Liveness — process ayakta mi? (check'ler atlanir)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness — yalnizca 'ready' tag'li check'ler
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Kargoyeri.Studio host beklenmedik sekilde sonlandi.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
