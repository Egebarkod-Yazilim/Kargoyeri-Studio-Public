namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels;

/// <summary>
/// DI'a kayitli tum IOrderChannelAdapter implementasyonlarini channel type'a gore arar.
/// Her channel icin tek bir adapter olur.
/// </summary>
public sealed class OrderChannelRegistry
{
    private readonly IReadOnlyDictionary<OrderChannelType, IOrderChannelAdapter> _byType;

    public OrderChannelRegistry(IEnumerable<IOrderChannelAdapter> adapters)
    {
        _byType = adapters.GroupBy(a => a.Type)
                          .ToDictionary(g => g.Key, g => g.FirstOrDefault(a => a is not Adapters.SimulationOrderChannelAdapterBase) ?? g.First());
    }

    public bool TryGet(OrderChannelType type, out IOrderChannelAdapter adapter)
    {
        if (_byType.TryGetValue(type, out var found))
        {
            adapter = found;
            return true;
        }
        adapter = null!;
        return false;
    }

    public IOrderChannelAdapter Require(OrderChannelType type) =>
        _byType.TryGetValue(type, out var a)
            ? a
            : throw new InvalidOperationException($"Order channel adapter kayitli degil: {type}");
}
