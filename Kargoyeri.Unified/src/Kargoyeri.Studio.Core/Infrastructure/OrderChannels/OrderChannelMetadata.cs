using Kargoyeri.Contracts.Dtos;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels;

/// <summary>
/// Per-channel metadata namespace: "channel.{code}.{field}".
/// Ornk: channel.hepsiburada.merchantId, channel.trendyol.apiKey
/// Ek alanlar:
///   channel.{code}.enabled        — bool
///   channel.{code}.lastSyncUtc    — ISO timestamp (son basarili sync)
///   channel.{code}.defaultProvider — kargo firmasi kodu (siparis cekildiginde varsayilan)
/// </summary>
public sealed class OrderChannelState
{
    public OrderChannelType Channel { get; init; }
    public bool Enabled { get; init; }
    public DateTimeOffset? LastSyncUtc { get; init; }
    public string? DefaultProvider { get; init; }
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();

    public bool HasRequiredFields(OrderChannelDescriptor descriptor) =>
        descriptor.Fields.Where(f => f.Required).All(f => Fields.ContainsKey(f.Key) && !string.IsNullOrWhiteSpace(Fields[f.Key]));
}

public static class OrderChannelMetadata
{
    private static string Prefix(OrderChannelType channel)
    {
        var d = OrderChannelCatalog.Find(channel);
        return $"channel.{d?.Code ?? channel.ToString().ToLowerInvariant()}.";
    }

    public static OrderChannelState Read(CustomerProfileDto? profile, OrderChannelType channel)
    {
        if (profile is null) return new OrderChannelState { Channel = channel };
        var prefix = Prefix(channel);
        var descriptor = OrderChannelCatalog.Find(channel);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (descriptor is not null)
        {
            foreach (var field in descriptor.Fields)
            {
                if (profile.Metadata.TryGetValue(prefix + field.Key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    fields[field.Key] = value;
                }
            }
        }

        DateTimeOffset? lastSync = null;
        if (profile.Metadata.TryGetValue(prefix + "lastSyncUtc", out var lastSyncStr) &&
            DateTimeOffset.TryParse(lastSyncStr, out var parsed))
        {
            lastSync = parsed;
        }

        return new OrderChannelState
        {
            Channel = channel,
            Enabled = profile.Metadata.TryGetValue(prefix + "enabled", out var en) && bool.TryParse(en, out var b) && b,
            LastSyncUtc = lastSync,
            DefaultProvider = profile.Metadata.TryGetValue(prefix + "defaultProvider", out var dp) ? dp : null,
            Fields = fields
        };
    }

    public static IReadOnlyList<OrderChannelState> ReadAll(CustomerProfileDto? profile) =>
        OrderChannelCatalog.All.Select(d => Read(profile, d.Type)).ToList();

    public static void Apply(
        Dictionary<string, string> metadata,
        OrderChannelType channel,
        bool enabled,
        string? defaultProvider,
        IReadOnlyDictionary<string, string> fieldValues)
    {
        var descriptor = OrderChannelCatalog.Find(channel)
            ?? throw new ArgumentException($"Bilinmeyen channel: {channel}");
        var prefix = Prefix(channel);

        metadata[prefix + "enabled"] = enabled ? "true" : "false";

        if (string.IsNullOrWhiteSpace(defaultProvider))
            metadata.Remove(prefix + "defaultProvider");
        else
            metadata[prefix + "defaultProvider"] = defaultProvider.Trim();

        foreach (var field in descriptor.Fields)
        {
            var key = prefix + field.Key;
            if (fieldValues.TryGetValue(field.Key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value.Trim();
            }
            else if (!field.Secret)
            {
                // Secret degerleri sifirlamak istemiyorsak: bos gelirse koru.
                // Non-secret ve bos gelirse: temizle.
                metadata.Remove(key);
            }
            // Secret + bos = mevcut deger korunur (kullanici "degistirme" demis demektir)
        }
    }

    public static void StampSync(Dictionary<string, string> metadata, OrderChannelType channel, DateTimeOffset whenUtc)
    {
        metadata[Prefix(channel) + "lastSyncUtc"] = whenUtc.ToString("O");
    }
}
