using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
	Task UpsertAsync(CustomerTenant customer, CancellationToken cancellationToken);

	Task<CustomerTenant?> GetByTenantKeyAsync(string tenantKey, CancellationToken cancellationToken);

	Task<CustomerTenant?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken);

	Task<IReadOnlyCollection<CustomerTenant>> ListAsync(CancellationToken cancellationToken);
}
