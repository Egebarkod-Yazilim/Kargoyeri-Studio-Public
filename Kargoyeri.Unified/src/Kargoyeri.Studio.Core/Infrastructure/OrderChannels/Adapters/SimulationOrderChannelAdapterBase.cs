namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Tum simulasyon-modlu adapter'lar icin ortak base.
/// Gercek API hazir olunca turetmis sinif override eder.
/// </summary>
public abstract class SimulationOrderChannelAdapterBase : IOrderChannelAdapter
{
    public abstract OrderChannelType Type { get; }

    protected virtual string DisplayName =>
        OrderChannelCatalog.Find(Type)?.DisplayName ?? Type.ToString();

    public virtual Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken cancellationToken)
    {
        var descriptor = OrderChannelCatalog.Find(Type);
        if (descriptor is null)
        {
            return Task.FromResult(new OrderChannelTestResult(false, $"Tanimsiz kanal: {Type}"));
        }

        foreach (var field in descriptor.Fields.Where(f => f.Required))
        {
            if (string.IsNullOrWhiteSpace(credentials.Get(field.Key)))
            {
                return Task.FromResult(new OrderChannelTestResult(
                    false, $"{field.Label} alani zorunlu — eksik."));
            }
        }

        return Task.FromResult(new OrderChannelTestResult(
            true,
            $"[Simulasyon] {DisplayName} bilgileri kabul edildi.",
            "Gercek API cagrisi yapilmadi. Production icin canli adapter aktive edilmelidir."));
    }

    public virtual Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken cancellationToken)
    {
        // Deterministic seed: aynı tenant + aynı saat penceresi = aynı sahte sipariş
        var seed = HashCode.Combine(credentials.TenantKey, Type, since.UtcDateTime.Date.Day);
        var rnd = new Random(seed);
        var count = rnd.Next(1, 4);
        var orders = new List<RawIncomingOrder>(count);

        for (int i = 0; i < count; i++)
        {
            var orderNo = $"SIM-{Type.ToString().ToUpperInvariant()[..Math.Min(3, Type.ToString().Length)]}-{rnd.Next(100000, 999999)}";
            orders.Add(new RawIncomingOrder(
                SourceChannel: Type,
                ChannelExternalOrderId: $"{credentials.TenantKey}:{Type}:{orderNo}",
                OrderNumber: orderNo,
                CreatedAtUtc: since.AddMinutes(rnd.Next(1, 60)),
                CollectionAmount: null,
                CurrencyCode: "TRY",
                Recipient: new RawAddress(
                    FullName: $"Sim Musteri {rnd.Next(1, 999)}",
                    Phone: "+90555" + rnd.Next(1000000, 9999999).ToString(),
                    Email: null,
                    City: "Istanbul",
                    District: "Kadikoy",
                    AddressLine: "Simulasyon Adresi No:" + rnd.Next(1, 200)),
                Items: new[]
                {
                    new RawOrderItem("Sim Urun", rnd.Next(1, 4), 49.90m, Sku: "SKU-" + rnd.Next(1000, 9999))
                },
                Notes: $"[SIMULATION] {DisplayName} test siparisi"));
        }

        return Task.FromResult(new OrderChannelFetchResult(
            true,
            $"[Simulasyon] {orders.Count} test siparisi uretildi.",
            orders,
            IsSimulation: true));
    }
}

public sealed class HepsiburadaOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.Hepsiburada;
}

public sealed class TrendyolOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.Trendyol;
}

public sealed class N11OrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.N11;
}

public sealed class CicekSepetiOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.CicekSepeti;
}

public sealed class GittiGidiyorOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.GittiGidiyor;
}

public sealed class PazaramaOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.Pazarama;
}

public sealed class AmazonOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.Amazon;
}

public sealed class NopCommerceOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.NopCommerce;
}

public sealed class PttAvmOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.PttAvm;
}

public sealed class ModanisaOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.Modanisa;
}

/// <summary>
/// Gomulu mini-shop — proje henuz hazir degil. Her zaman "yakinda" doner.
/// </summary>
public sealed class EmbeddedShopOrderChannelAdapter : IOrderChannelAdapter
{
    public OrderChannelType Type => OrderChannelType.EmbeddedShop;

    public Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken cancellationToken) =>
        Task.FromResult(new OrderChannelTestResult(
            false,
            "Gomulu mini-shop henuz aktif degil. Aktivasyon icin hesap yoneticinizle iletisime gecin.",
            "ComingSoon"));

    public Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken cancellationToken) =>
        Task.FromResult(new OrderChannelFetchResult(
            false,
            "Mini-shop fetch endpoint'i bu surumde aktif degil.",
            Array.Empty<RawIncomingOrder>(),
            IsSimulation: true));
}
