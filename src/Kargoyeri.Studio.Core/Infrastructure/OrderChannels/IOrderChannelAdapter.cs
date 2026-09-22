namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels;

/// <summary>
/// Bir order channel'in (Hepsiburada, Trendyol, NopCommerce, ...)
/// fetch + connect-test contract'i.
/// </summary>
public interface IOrderChannelAdapter
{
    OrderChannelType Type { get; }

    /// <summary>API erisilebilir mi? Credential dogru mu?</summary>
    Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials,
        CancellationToken cancellationToken);

    /// <summary>
    /// Verilen tarihten sonra olusan siparisleri ceker. Her satir RawIncomingOrder.
    /// Idempotent olmali — caller ChannelExternalOrderId'ye gore dedup eder.
    /// </summary>
    Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials,
        DateTimeOffset since,
        CancellationToken cancellationToken);
}

public sealed record OrderChannelCredentials(
    string TenantKey,
    OrderChannelType Channel,
    IReadOnlyDictionary<string, string> Fields)
{
    public string? Get(string key) =>
        Fields.TryGetValue(key, out var value) ? value : null;
}

public sealed record OrderChannelTestResult(
    bool Success,
    string Message,
    string? Detail = null);

public sealed record OrderChannelFetchResult(
    bool Success,
    string Message,
    IReadOnlyList<RawIncomingOrder> Orders,
    bool IsSimulation = false);

/// <summary>
/// Channel-bagimsiz ham siparis. SyncEngine bunu CargoShipment'a cevirir.
/// </summary>
public sealed record RawIncomingOrder(
    OrderChannelType SourceChannel,   // Hangi kanaldan geldi — adapter kendi Channel'ini set eder
    string ChannelExternalOrderId,    // Channel'daki uniq ID — idempotency icin
    string OrderNumber,
    DateTimeOffset CreatedAtUtc,
    decimal? CollectionAmount,
    string CurrencyCode,
    RawAddress Recipient,
    IReadOnlyList<RawOrderItem> Items,
    string? Notes = null,
    IReadOnlyDictionary<string, string>? RawMetadata = null);

public sealed record RawAddress(
    string FullName,
    string? Phone,
    string? Email,
    string? City,
    string? District,
    string? AddressLine,
    string? PostalCode = null);

public sealed record RawOrderItem(
    string ProductName,
    int Quantity,
    decimal? UnitPrice,
    string? Sku = null,
    decimal? WeightKg = null);
