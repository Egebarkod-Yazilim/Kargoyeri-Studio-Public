using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class SqlProviderSettingsRepository : IProviderSettingsRepository
{
	private readonly KargoyeriDbContext _db;

	public SqlProviderSettingsRepository(KargoyeriDbContext db)
	{
		_db = db;
	}

	public async Task UpsertAsync(ProviderCredential settings, CancellationToken cancellationToken)
	{
		ProviderCredential settings2 = settings;
		ProviderCredential existing = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<ProviderCredential>((IQueryable<ProviderCredential>)_db.ProviderCredentials, (Expression<Func<ProviderCredential, bool>>)((ProviderCredential x) => x.TenantKey == settings2.TenantKey && (int)x.Provider == (int)settings2.Provider), cancellationToken);
		if (existing == null)
		{
			_db.ProviderCredentials.Add(settings2);
			return;
		}
		existing.IsEnabled = settings2.IsEnabled;
		existing.ClientCode = settings2.ClientCode;
		existing.Username = settings2.Username;
		existing.Password = settings2.Password;
		existing.ApiKey = settings2.ApiKey;
		existing.EndpointBase = settings2.EndpointBase;
		existing.AdditionalSettings = settings2.AdditionalSettings;
		existing.UpdatedAtUtc = settings2.UpdatedAtUtc;
	}

	public async Task<ProviderCredential?> GetAsync(string tenantKey, CargoProviderType provider, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<ProviderCredential>((IQueryable<ProviderCredential>)_db.ProviderCredentials, (Expression<Func<ProviderCredential, bool>>)((ProviderCredential x) => x.TenantKey == tenantKey2 && (int)x.Provider == (int)provider), cancellationToken);
	}
}
