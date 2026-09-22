using Kargoyeri.Contracts.Providers;
using Kargoyeri.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Kargoyeri.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Kargoyeri altyapı katmanını DI'a kaydeder.
    /// Studio.Web ve Embedded host'un her ikisi de bunu çağırır.
    /// </summary>
    public static IServiceCollection AddKargoyeriInfrastructure(
        this IServiceCollection services)
    {
        // ─── Provider bağlantı deposu ──────────────────────────────────────
        // Faz 1: bellek içi (restart'ta sıfırlanır)
        // Faz 2: services.AddScoped<IProviderConnectionStore, EfProviderConnectionStore>();
        services.AddSingleton<IProviderConnectionStore, InMemoryProviderConnectionStore>();

        // ─── Provider'ları kaydet ──────────────────────────────────────────
        // Her yeni provider için buraya bir satır ekle.
        services.AddSingleton<ICargoProvider, MockCargoProvider>();

        // MNG Faz 2 — transport hazır olunca uncomment et:
        // services.AddHttpClient<MngCargoProvider>();
        // services.AddSingleton<ICargoProvider, MngCargoProvider>();

        // ─── Registry (provider koleksiyonunu toplar) ──────────────────────
        services.AddSingleton<IProviderRegistry, ProviderRegistry>();

        return services;
    }
}
