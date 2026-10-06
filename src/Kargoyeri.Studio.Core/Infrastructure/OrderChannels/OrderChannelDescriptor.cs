namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels;

/// <summary>
/// Bir order channel'in UI ve credential semasi tanimi.
/// Her channel kendi alan setini (apiKey, apiSecret, supplierId, ...) bildirir.
/// </summary>
public sealed record OrderChannelDescriptor(
    OrderChannelType Type,
    string Code,
    string DisplayName,
    string Group,
    string Tagline,
    OrderChannelStage Stage,
    IReadOnlyList<OrderChannelField> Fields,
    string? DocsUrl = null);

public sealed record OrderChannelField(
    string Key,
    string Label,
    string Placeholder,
    bool Required,
    bool Secret = false,
    string? Hint = null);

/// <summary>
/// Tum desteklenen channel'lerin merkezi katalog tanimi.
/// Yeni bir channel eklemek icin sadece buraya yeni descriptor + adapter implementasyonu eklemek yeterlidir.
/// </summary>
public static class OrderChannelCatalog
{
    private static readonly OrderChannelDescriptor[] _all = new[]
    {
        // ── Marketplace ──────────────────────────────────────────────
        new OrderChannelDescriptor(
            OrderChannelType.Hepsiburada,
            "hepsiburada",
            "Hepsiburada",
            "Pazaryeri",
            "Hepsiburada Marketplace API",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("merchantId", "Merchant ID", "Hepsiburada satici ID", true, Hint: "Satici Paneli > Entegrasyonlar > Merchant ID"),
                new("username",   "API Kullanici Adi", "API kullanici adi", true),
                new("password",   "API Sifresi", "API sifresi", true, Secret: true),
            },
            DocsUrl: "https://developers.hepsiburada.com"),

        new OrderChannelDescriptor(
            OrderChannelType.Trendyol,
            "trendyol",
            "Trendyol",
            "Pazaryeri",
            "Trendyol Seller Center API",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("supplierId", "Supplier ID",   "Trendyol satici ID", true),
                new("apiKey",     "API Key",       "Seller portal > Entegrasyonlar", true, Secret: true),
                new("apiSecret",  "API Secret",    "Seller portal > Entegrasyonlar", true, Secret: true),
            },
            DocsUrl: "https://developers.trendyol.com"),

        new OrderChannelDescriptor(
            OrderChannelType.N11,
            "n11",
            "N11",
            "Pazaryeri",
            "N11 Marketplace API (SOAP)",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("apiKey",     "API Key",     "N11 satici paneli", true, Secret: true),
                new("apiSecret",  "API Secret",  "N11 satici paneli", true, Secret: true),
            },
            DocsUrl: "https://api.n11.com"),

        new OrderChannelDescriptor(
            OrderChannelType.CicekSepeti,
            "ciceksepeti",
            "Cicek Sepeti",
            "Pazaryeri",
            "Cicek Sepeti Marketplace API",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("apiKey",     "API Key",     "satici@ciceksepeti.com'dan istenmesi gerekir", true, Secret: true),
                new("dealerCode", "Bayi Kodu",   "CS bayi/satici kodu", true),
            },
            DocsUrl: "https://apidocs.ciceksepeti.com"),

        new OrderChannelDescriptor(
            OrderChannelType.GittiGidiyor,
            "gittigidiyor",
            "GittiGidiyor",
            "Pazaryeri",
            "GittiGidiyor 2024'te kapandı — bu kanal artık seçilemez (arşiv)",
            OrderChannelStage.ComingSoon,
            new OrderChannelField[]
            {
                new("apiKey",     "API Key",     "GG satici paneli > API", true, Secret: true),
                new("apiSecret",  "API Secret",  "GG satici paneli > API", true, Secret: true),
            }),

        new OrderChannelDescriptor(
            OrderChannelType.Pazarama,
            "pazarama",
            "Pazarama",
            "Pazaryeri",
            "Pazarama Marketplace API",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("clientId",     "Client ID",     "Pazarama satici paneli", true),
                new("clientSecret", "Client Secret", "Pazarama satici paneli", true, Secret: true),
            }),

        new OrderChannelDescriptor(
            OrderChannelType.Amazon,
            "amazon",
            "Amazon TR",
            "Pazaryeri",
            "Amazon SP-API (Selling Partner)",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("sellerId",     "Seller ID",     "Amazon Seller Central", true),
                new("refreshToken", "LWA Refresh Token", "OAuth refresh token", true, Secret: true),
                new("accessKeyId",  "AWS Access Key ID", "IAM kullanici (sadece SP-API)", true),
                new("secretKey",    "AWS Secret Key", "IAM kullanici secret", true, Secret: true),
            }),

        new OrderChannelDescriptor(
            OrderChannelType.PttAvm,
            "pttavm",
            "PttAVM",
            "Pazaryeri",
            "PttAVM Marketplace API (PTT işbirliği gerekir)",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("apiKey",    "API Key",     "PttAVM satıcı paneli > Entegrasyon", true, Secret: true),
                new("sellerId",  "Satıcı Kodu", "PttAVM tarafından atanan satıcı kodu", true),
            },
            DocsUrl: "https://www.pttavm.com"),

        new OrderChannelDescriptor(
            OrderChannelType.Modanisa,
            "modanisa",
            "Modanisa",
            "Pazaryeri",
            "Modanisa Marketplace API (anlaşmalı satıcılar)",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("apiKey",    "API Key",     "Modanisa satıcı paneli", true, Secret: true),
                new("apiSecret", "API Secret",  "Modanisa satıcı paneli", true, Secret: true),
                new("sellerId",  "Satıcı ID",   "Modanisa numerik satıcı ID'si", true),
            },
            DocsUrl: "https://merchant.modanisa.com"),

        // ── E-Ticaret Platformlari ───────────────────────────────────
        new OrderChannelDescriptor(
            OrderChannelType.NopCommerce,
            "nopcommerce",
            "NopCommerce",
            "E-Ticaret",
            "Inroen Nop API ile siparis cek (GET /api/orders)",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("siteUrl",  "Magaza URL",  "https://test.egebarkod.com", true,
                    Hint: "Swagger: /swagger/index.html — ornek test ortami test.egebarkod.com"),
                new("username", "E-posta / Kullanici adi", "Login e-postasi veya kullanici adi", true),
                new("password", "Sifre", "POST /api/auth/login sifresi", true, Secret: true),
                new("apiKey", "API Anahtari", "X-Api-Key (opsiyonel alternatif)", false, Secret: true,
                    Hint: "Doluysa Bearer login yerine veya yaninda X-Api-Key header gider"),
                new("orderStatusId", "Siparis durum ID", "20 = Processing, bos = tumu", false,
                    Hint: "Nop: 10 Pending, 20 Processing, 30 Complete, 40 Cancelled"),
            },
            DocsUrl: "https://test.egebarkod.com/swagger/index.html"),

        new OrderChannelDescriptor(
            OrderChannelType.Shopify,
            "shopify",
            "Shopify",
            "E-Ticaret",
            "Shopify Admin REST API (Custom App veya Public App token)",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("shopDomain",  "Shop Domain",   "magaza-adim.myshopify.com", true,
                    Hint: ".myshopify.com otomatik eklenir"),
                new("accessToken", "Access Token",  "shpat_... (Custom App admin token)", true, Secret: true),
                new("apiVersion",  "API Versiyonu", "2024-10 (default)", false,
                    Hint: "Bos birakirsaniz 2024-10 kullanilir"),
            },
            DocsUrl: "https://shopify.dev/docs/api/admin-rest"),

        new OrderChannelDescriptor(
            OrderChannelType.WooCommerce,
            "woocommerce",
            "WooCommerce",
            "E-Ticaret",
            "WooCommerce REST API v3 (WordPress plugin)",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("siteUrl",        "Site URL",        "https://magaza.com", true,
                    Hint: "WordPress kurulu olan ana adres"),
                new("consumerKey",    "Consumer Key",    "ck_...", true,
                    Hint: "WooCommerce > Settings > Advanced > REST API"),
                new("consumerSecret", "Consumer Secret", "cs_...", true, Secret: true),
            },
            DocsUrl: "https://woocommerce.github.io/woocommerce-rest-api-docs/"),

        new OrderChannelDescriptor(
            OrderChannelType.Ticimax,
            "ticimax",
            "Ticimax",
            "E-Ticaret",
            "Ticimax JSON RPC API — TR e-ticaret altyapisi",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("siteUrl", "Site URL", "https://magaza.com", true,
                    Hint: "/Servis/SiparisServis.svc otomatik eklenir"),
                new("apiKey",  "API Key (UyeKodu)", "Ticimax panel > API Ayarlari", true, Secret: true),
            },
            DocsUrl: "https://ticimax.com.tr/destek-merkezi"),

        new OrderChannelDescriptor(
            OrderChannelType.IdeaSoft,
            "ideasoft",
            "IdeaSoft",
            "E-Ticaret",
            "IdeaSoft REST API (OAuth2 Client Credentials)",
            OrderChannelStage.Live,
            new OrderChannelField[]
            {
                new("storeUrl",     "Magaza URL",     "https://magaza.com", true),
                new("clientId",     "Client ID",      "IdeaSoft API > Uygulamalar", true),
                new("clientSecret", "Client Secret",  "IdeaSoft API > Uygulamalar", true, Secret: true),
            },
            DocsUrl: "https://developer.ideasoft.com.tr"),

        // ── Gomulu mini-shop ─────────────────────────────────────────
        new OrderChannelDescriptor(
            OrderChannelType.EmbeddedShop,
            "embedded",
            "Gomulu Mini-Shop",
            "Studio Yerlesik",
            "Studio icindeki kucuk mini-magaza (henuz aktif degil)",
            OrderChannelStage.ComingSoon,
            Array.Empty<OrderChannelField>())
    };

    public static IReadOnlyList<OrderChannelDescriptor> All => _all;

    public static OrderChannelDescriptor? Find(OrderChannelType type) =>
        _all.FirstOrDefault(d => d.Type == type);

    public static OrderChannelDescriptor? FindByCode(string code) =>
        _all.FirstOrDefault(d => string.Equals(d.Code, code, StringComparison.OrdinalIgnoreCase));
}
