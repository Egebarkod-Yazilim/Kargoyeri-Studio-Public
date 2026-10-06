// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Services;

public sealed partial class ProviderIntegrationStatusService
{
    public ProviderIntegrationStatusService(global::Kargoyeri.Application.Abstractions.Persistence.IProviderSettingsRepository providerSettingsRepository, global::Kargoyeri.Application.Services.ProviderCatalogService providerCatalogService, global::Kargoyeri.Application.Services.CustomerService customerService) { }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.ProviderIntegrationStatusDto> GetAsync(string customerCode, global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto provider, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
}
