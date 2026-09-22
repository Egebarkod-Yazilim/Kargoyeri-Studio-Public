using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonSessionCustomerRepository : ICustomerRepository
{
	private readonly JsonDatabaseSession _session;

	public JsonSessionCustomerRepository(JsonDatabaseSession session)
	{
		_session = session;
	}

	public async Task UpsertAsync(CustomerTenant customer, CancellationToken cancellationToken)
	{
		CustomerTenant customer2 = customer;
		JsonDatabaseDocument document = await _session.GetDocumentAsync(cancellationToken);
		int index = document.Customers.FindIndex((CustomerTenant x) => x.TenantKey == customer2.TenantKey);
		if (index >= 0)
		{
			document.Customers[index] = customer2;
		}
		else
		{
			document.Customers.Add(customer2);
		}
	}

	public async Task<CustomerTenant?> GetByTenantKeyAsync(string tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		return (await _session.GetDocumentAsync(cancellationToken)).Customers.FirstOrDefault((CustomerTenant x) => x.TenantKey == tenantKey2);
	}

	public async Task<CustomerTenant?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken)
	{
		string apiKeyHash2 = apiKeyHash;
		return (await _session.GetDocumentAsync(cancellationToken)).Customers.FirstOrDefault((CustomerTenant x) => x.ApiKeyHash == apiKeyHash2);
	}

	public async Task<IReadOnlyCollection<CustomerTenant>> ListAsync(CancellationToken cancellationToken)
	{
		return (IReadOnlyCollection<CustomerTenant>)(object)(await _session.GetDocumentAsync(cancellationToken)).Customers.ToArray();
	}
}
