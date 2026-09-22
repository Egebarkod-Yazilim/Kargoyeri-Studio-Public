using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonSessionProviderSettingsRepository : IProviderSettingsRepository
{
	private readonly JsonDatabaseSession _session;

	public JsonSessionProviderSettingsRepository(JsonDatabaseSession session)
	{
		_session = session;
	}

	public async Task UpsertAsync(ProviderCredential settings, CancellationToken cancellationToken)
	{
		ProviderCredential settings2 = settings;
		JsonDatabaseDocument document = await _session.GetDocumentAsync(cancellationToken);
		int index = document.ProviderSettings.FindIndex((ProviderCredential x) => x.TenantKey == settings2.TenantKey && x.Provider == settings2.Provider);
		if (index >= 0)
		{
			document.ProviderSettings[index] = settings2;
		}
		else
		{
			document.ProviderSettings.Add(settings2);
		}
	}

	public async Task<ProviderCredential?> GetAsync(string tenantKey, CargoProviderType provider, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		return (await _session.GetDocumentAsync(cancellationToken)).ProviderSettings.FirstOrDefault((ProviderCredential x) => x.TenantKey == tenantKey2 && x.Provider == provider);
	}
}
