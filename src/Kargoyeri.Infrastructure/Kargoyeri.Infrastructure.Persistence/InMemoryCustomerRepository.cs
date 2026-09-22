using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class InMemoryCustomerRepository : ICustomerRepository
{
	private readonly List<CustomerTenant> _customers = new List<CustomerTenant>();

	private readonly object _lock = new object();

	public Task UpsertAsync(CustomerTenant customer, CancellationToken cancellationToken)
	{
		CustomerTenant customer2 = customer;
		lock (_lock)
		{
			int num = _customers.FindIndex((CustomerTenant x) => x.TenantKey == customer2.TenantKey);
			if (num >= 0)
			{
				_customers[num] = customer2;
			}
			else
			{
				_customers.Add(customer2);
			}
		}
		return Task.CompletedTask;
	}

	public Task<CustomerTenant?> GetByTenantKeyAsync(string tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		lock (_lock)
		{
			return Task.FromResult(_customers.FirstOrDefault((CustomerTenant x) => x.TenantKey == tenantKey2));
		}
	}

	public Task<CustomerTenant?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken)
	{
		string apiKeyHash2 = apiKeyHash;
		lock (_lock)
		{
			return Task.FromResult(_customers.FirstOrDefault((CustomerTenant x) => x.ApiKeyHash == apiKeyHash2));
		}
	}

	public Task<IReadOnlyCollection<CustomerTenant>> ListAsync(CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			return Task.FromResult((IReadOnlyCollection<CustomerTenant>)(object)_customers.ToArray());
		}
	}
}
