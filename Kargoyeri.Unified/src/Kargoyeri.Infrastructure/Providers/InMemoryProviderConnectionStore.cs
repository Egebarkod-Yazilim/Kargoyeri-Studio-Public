using Kargoyeri.Contracts.Providers;

namespace Kargoyeri.Infrastructure.Providers;

/// <summary>
/// Geliştirme ve demo için bellek içi bağlantı deposu.
/// Faz 2'de EF Core / SQLite implementasyonu ile değiştirilir.
/// </summary>
public sealed class InMemoryProviderConnectionStore : IProviderConnectionStore
{
    private readonly Dictionary<string, ProviderConnection> _store = new();

    private static string Key(string workspaceId, string providerId) =>
        $"{workspaceId}::{providerId}";

    public Task<ProviderConnection?> GetAsync(string workspaceId, string providerId, CancellationToken ct = default)
    {
        _store.TryGetValue(Key(workspaceId, providerId), out var conn);
        return Task.FromResult(conn);
    }

    public Task SaveAsync(ProviderConnection connection, CancellationToken ct = default)
    {
        _store[Key(connection.WorkspaceId, connection.ProviderId)] = connection;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProviderConnection>> GetAllForWorkspaceAsync(string workspaceId, CancellationToken ct = default)
    {
        var result = _store.Values
            .Where(c => c.WorkspaceId == workspaceId && c.IsActive)
            .ToList();
        return Task.FromResult<IReadOnlyList<ProviderConnection>>(result);
    }

    public Task DeleteAsync(string workspaceId, string providerId, CancellationToken ct = default)
    {
        _store.Remove(Key(workspaceId, providerId));
        return Task.CompletedTask;
    }
}
