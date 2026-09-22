namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels;

/// <summary>
/// Bir musterinin sipariş kaynağı olarak ekleyebileceği kanallar.
/// 3 ana senaryo: Marketplace API'leri, NopCommerce, gomulu mini-shop.
/// Manuel giris kanal sayilmaz; her zaman aktiftir.
/// </summary>
public enum OrderChannelType
{
    // ── Marketplace API'leri (Senaryo 1) ───────────────────────────
    Hepsiburada = 1,
    Trendyol = 2,
    N11 = 3,
    CicekSepeti = 4,
    GittiGidiyor = 5,
    Pazarama = 6,
    Amazon = 7,
    PttAvm = 8,
    Modanisa = 9,

    // ── NopCommerce (Senaryo 2) ─────────────────────────────────────
    NopCommerce = 50,

    // ── E-ticaret platformlari ──────────────────────────────────────
    Shopify     = 60,
    WooCommerce = 61,
    Ticimax     = 62,
    IdeaSoft    = 63,

    // ── Gomulu mini-shop (Senaryo 3, henuz hazir degil) ─────────────
    EmbeddedShop = 90,

    // ── Operatorun elle eklediği siparişler ─────────────────────────
    Manual = 95,

    // ── Tanimlanmamis 3rd-party / generic ───────────────────────────
    Custom = 99
}

public enum OrderChannelStage
{
    /// <summary>API entegrasyonu canli ve kullanima hazir.</summary>
    Live = 1,

    /// <summary>API kontrati var ama simulasyon modunda — gercek istek atmaz.</summary>
    Simulation = 2,

    /// <summary>UI hazir, backend onceki bir surumde gelecek.</summary>
    ComingSoon = 3
}
