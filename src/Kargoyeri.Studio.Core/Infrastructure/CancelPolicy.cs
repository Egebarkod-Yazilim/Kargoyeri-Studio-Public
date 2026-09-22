using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Gonderi iptal kurallari — Studio katmaninda tek noktadan tutulur.
/// Hangi durumdan iptal edilebilir, hangi provider iptali destekler.
/// </summary>
public static class CancelPolicy
{
    /// <summary>Bu durumlarda iptal mantikli ve guvenli.</summary>
    public static readonly IReadOnlySet<string> CancellableStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Pending",
            "Queued",
            "ProviderAccepted",
            "LabelReady",
            "InTransit",
            "Failed"     // basarisiz olusturulan da temizlenebilsin
        };

    /// <summary>Bu durumlarda iptal anlamsiz/imkansiz.</summary>
    public static readonly IReadOnlySet<string> NonCancellableStatuses =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Delivered",
            "Cancelled"
        };

    /// <summary>
    /// Otomatik iptal API'si DESTEKLEMEYEN kargo firmalari.
    /// Bu firmalarin gonderileri musteri panelinden / cagri merkezinden iptal edilir.
    /// (PTT API iptal desteklemiyor; Trendyol Express paket iptali Seller Panel'den.)
    /// </summary>
    public static readonly IReadOnlySet<CargoProviderTypeDto> UnsupportedProviders =
        new HashSet<CargoProviderTypeDto>
        {
            CargoProviderTypeDto.Ptt,
            CargoProviderTypeDto.TrendyolExpress
        };

    public static bool IsCancellable(string? status) =>
        !string.IsNullOrWhiteSpace(status) && CancellableStatuses.Contains(status);

    public static bool IsCancellable(ShipmentStatusDto status) =>
        IsCancellable(status.ToString());

    public static bool IsProviderSupported(CargoProviderTypeDto provider) =>
        !UnsupportedProviders.Contains(provider);

    /// <summary>UI'da kullanici bilgilendirme metni.</summary>
    public static string GetUnsupportedReason(CargoProviderTypeDto provider) => provider switch
    {
        CargoProviderTypeDto.Ptt =>
            "PTT Kargo otomatik iptali desteklemiyor. Iptal icin musteri hizmetleri (444 1 PTT) ile iletisime gecin.",
        CargoProviderTypeDto.TrendyolExpress =>
            "Trendyol Express paket iptalleri Seller Panel uzerinden yapilir.",
        _ => "Bu kargo firmasi otomatik iptali desteklemiyor."
    };
}
