using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kargoyeri.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    // Bridge the newer Studio host to the older infrastructure registration surface.
    public static IServiceCollection AddKargoyeriCore(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        return Kargoyeri.Infrastructure.Extensions.ServiceCollectionExtensions
            .AddKargoyeriInfrastructure(services)
            .AddRecoveredApplicationRuntime();
    }
}
