using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class SqlCustomerRepository : ICustomerRepository
{
	private readonly KargoyeriDbContext _db;

	public SqlCustomerRepository(KargoyeriDbContext db)
	{
		_db = db;
	}

	public async Task UpsertAsync(CustomerTenant customer, CancellationToken cancellationToken)
	{
		CustomerTenant customer2 = customer;
		CustomerTenant existing = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<CustomerTenant>((IQueryable<CustomerTenant>)_db.Customers, (Expression<Func<CustomerTenant, bool>>)((CustomerTenant x) => x.TenantKey == customer2.TenantKey), cancellationToken);
		if (existing == null)
		{
			_db.Customers.Add(customer2);
			return;
		}
		existing.Name = customer2.Name;
		existing.IsActive = customer2.IsActive;
		existing.ApiKeyHash = customer2.ApiKeyHash;
		existing.AllowedProviders = customer2.AllowedProviders;
		existing.NotificationTargets = customer2.NotificationTargets;
		existing.Metadata = customer2.Metadata;
		existing.UpdatedAtUtc = customer2.UpdatedAtUtc;
	}

	public async Task<CustomerTenant?> GetByTenantKeyAsync(string tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<CustomerTenant>((IQueryable<CustomerTenant>)_db.Customers, (Expression<Func<CustomerTenant, bool>>)((CustomerTenant x) => x.TenantKey == tenantKey2), cancellationToken);
	}

	public async Task<CustomerTenant?> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken)
	{
		string apiKeyHash2 = apiKeyHash;
		return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<CustomerTenant>((IQueryable<CustomerTenant>)_db.Customers, (Expression<Func<CustomerTenant, bool>>)((CustomerTenant x) => x.ApiKeyHash == apiKeyHash2), cancellationToken);
	}

	public async Task<IReadOnlyCollection<CustomerTenant>> ListAsync(CancellationToken cancellationToken)
	{
		return (IReadOnlyCollection<CustomerTenant>)(object)(await EntityFrameworkQueryableExtensions.ToArrayAsync<CustomerTenant>((IQueryable<CustomerTenant>)((IQueryable<CustomerTenant>)_db.Customers).OrderBy((CustomerTenant x) => x.Name), cancellationToken));
	}
}
