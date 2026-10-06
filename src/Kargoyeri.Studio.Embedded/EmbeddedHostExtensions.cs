using Kargoyeri.Studio.Core;
using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kargoyeri.Studio.Embedded;

public static class EmbeddedHostExtensions
{
    /// <summary>
    /// Kargoyeri Studio'yu mevcut bir ASP.NET Core uygulamasina gomulu olarak ekler.
    /// Bu cagri builder.Services.AddControllersWithViews() SONRASINDA yapilmalidir.
    ///
    /// Ornek kullanim (parent projenin Program.cs):
    ///   builder.Services.AddControllersWithViews()
    ///       .AddApplicationPart(typeof(EmbeddedHostExtensions).Assembly); // gerekmez, AddKargoyeriStudioEmbedded otomatik ekler
    ///   builder.Services.AddKargoyeriStudioEmbedded(builder.Configuration, builder.Environment.ContentRootPath);
    /// </summary>
    public static IServiceCollection AddKargoyeriStudioEmbedded(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        // Core assembly'sindeki Controller ve View'lari parent uygulamaya tanit
        var coreAssembly = typeof(StudioServiceExtensions).Assembly;
        services.AddControllersWithViews()
            .AddApplicationPart(coreAssembly);

        // Cookie auth (parent uygulamanin zaten auth sistemi varsa bu blogu kaldir)
        services.Configure<StudioAccessOptions>(configuration.GetSection("StudioAccess"));
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Access/Login";
                options.AccessDeniedPath = "/Access/Login";
                options.Cookie.Name = "kargoyeri-studio-auth";
                options.SlidingExpiration = true;
            });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        // Tum Studio servislerini kaydet
        services.AddKargoyeriStudio(configuration, contentRootPath);

        return services;
    }
}
