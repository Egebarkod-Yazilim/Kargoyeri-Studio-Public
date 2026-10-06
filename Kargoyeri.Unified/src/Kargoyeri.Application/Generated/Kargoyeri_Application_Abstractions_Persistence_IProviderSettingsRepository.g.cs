// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Persistence;

public interface IProviderSettingsRepository
{
    global::System.Threading.Tasks.Task UpsertAsync(global::Kargoyeri.Domain.Entities.ProviderCredential settings, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.ProviderCredential> GetAsync(string tenantKey, global::Kargoyeri.Domain.Enums.CargoProviderType provider, global::System.Threading.CancellationToken cancellationToken);
}
