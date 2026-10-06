using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class InMemoryProviderSettingsRepository : IProviderSettingsRepository
{
	private readonly List<ProviderCredential> _settings = new List<ProviderCredential>();

	private readonly object _lock = new object();

	public Task UpsertAsync(ProviderCredential settings, CancellationToken cancellationToken)
	{
		ProviderCredential settings2 = settings;
		lock (_lock)
		{
			int num = _settings.FindIndex((ProviderCredential x) => x.Provider == settings2.Provider);
			if (num >= 0)
			{
				_settings[num] = settings2;
			}
			else
			{
				_settings.Add(settings2);
			}
		}
		return Task.CompletedTask;
	}

	public Task<ProviderCredential?> GetAsync(string tenantKey, CargoProviderType provider, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		lock (_lock)
		{
			return Task.FromResult(_settings.FirstOrDefault((ProviderCredential x) => x.TenantKey == tenantKey2 && x.Provider == provider));
		}
	}
}
