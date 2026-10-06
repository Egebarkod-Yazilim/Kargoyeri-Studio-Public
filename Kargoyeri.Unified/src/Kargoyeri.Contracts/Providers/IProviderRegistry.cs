namespace Kargoyeri.Contracts.Providers;

/// <summary>
/// Kayıtlı tüm provider'ları yönetir.
/// DI üzerinden çözülür; Studio.Web sayfaları bunu kullanır.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>Kayıtlı tüm provider'lar</summary>
    IReadOnlyList<ICargoProvider> GetAll();

    /// <summary>ID ile provider bul</summary>
    ICargoProvider? GetById(string providerId);

    /// <summary>Belirli bir capability'yi destekleyen provider'lar</summary>
    IReadOnlyList<ICargoProvider> GetByCapability(ProviderCapability capability);
}

/// <summary>
/// Bir kullanıcı/workspace için provider bağlantı durumu.
/// Gerçek uygulamada DB'den okunur; bu arayüz soyutlama sağlar.
/// </summary>
public interface IProviderConnectionStore
{
    Task<ProviderConnection?> GetAsync(string workspaceId, string providerId, CancellationToken ct = default);
    Task SaveAsync(ProviderConnection connection, CancellationToken ct = default);
    Task<IReadOnlyList<ProviderConnection>> GetAllForWorkspaceAsync(string workspaceId, CancellationToken ct = default);
    Task DeleteAsync(string workspaceId, string providerId, CancellationToken ct = default);
}

public record ProviderConnection(
    string WorkspaceId,
    string ProviderId,
    Dictionary<string, string> Credentials,
    string? AccountName,
    DateTimeOffset ConnectedAt,
    bool IsActive = true
);
