// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Services;

public sealed partial class ProviderCatalogService
{
    public ProviderCatalogService(global::System.Collections.Generic.IEnumerable<global::Kargoyeri.Application.Abstractions.Providers.IProviderBlueprint> blueprints, global::Kargoyeri.Application.Abstractions.Persistence.IProviderSettingsRepository providerSettingsRepository, global::Kargoyeri.Application.Services.CustomerService customerService) { }
    public global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Contracts.Dtos.ProviderProfileDto> List()
    {
        throw new global::System.NotImplementedException();
    }
    public global::Kargoyeri.Contracts.Dtos.ProviderProfileDto Get(global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto provider)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.ProviderRequestPreviewResponse> PreviewCreateAsync(string tenantKey, global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto provider, global::Kargoyeri.Contracts.Dtos.CreateShipmentRequest request, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
}
