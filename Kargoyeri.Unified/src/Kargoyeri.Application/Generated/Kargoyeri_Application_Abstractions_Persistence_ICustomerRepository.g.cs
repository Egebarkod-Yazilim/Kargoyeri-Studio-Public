// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    global::System.Threading.Tasks.Task UpsertAsync(global::Kargoyeri.Domain.Entities.CustomerTenant customer, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.CustomerTenant> GetByTenantKeyAsync(string tenantKey, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.CustomerTenant> GetByApiKeyHashAsync(string apiKeyHash, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Domain.Entities.CustomerTenant>> ListAsync(global::System.Threading.CancellationToken cancellationToken);
}
