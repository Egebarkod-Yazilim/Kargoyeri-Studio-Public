using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Application.Abstractions.Persistence;

public interface IProviderSettingsRepository
{
	Task UpsertAsync(ProviderCredential settings, CancellationToken cancellationToken);

	Task<ProviderCredential?> GetAsync(string tenantKey, CargoProviderType provider, CancellationToken cancellationToken);
}
