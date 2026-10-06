using Kargoyeri.Contracts.Providers;

namespace Kargoyeri.Infrastructure.Providers;

/// <summary>
/// DI container'a kayıtlı tüm ICargoProvider'ları toplar.
/// Yeni provider eklemek için sadece DI'a register et — başka değişiklik yok.
/// </summary>
public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<string, ICargoProvider> _providers;

    public ProviderRegistry(IEnumerable<ICargoProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ICargoProvider> GetAll() =>
        _providers.Values.ToList();

    public ICargoProvider? GetById(string providerId) =>
        _providers.TryGetValue(providerId, out var p) ? p : null;

    public IReadOnlyList<ICargoProvider> GetByCapability(ProviderCapability capability) =>
        _providers.Values
            .Where(p => p.Capabilities.Has(capability))
            .ToList();
}
