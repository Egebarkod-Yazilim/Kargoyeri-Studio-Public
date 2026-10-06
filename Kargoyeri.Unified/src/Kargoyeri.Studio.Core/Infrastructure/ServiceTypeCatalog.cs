using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Kargoyeri'nin standart servis seviyeleri. Provider-bagimsizdir; her sagiyici
/// kendi koduna <see cref="ServiceTypeCatalog"/> uzerinden cevirir.
/// </summary>
public enum KargoyeriServiceTier
{
    /// <summary>Standart kara teslimat (1-3 is gunu).</summary>
    Standard = 0,
    /// <summary>Hizli teslimat (1-2 is gunu).</summary>
    Express = 1,
    /// <summary>Ayni gun teslimat (sehir ici).</summary>
    SameDay = 2,
    /// <summary>Ertesi gun teslimat.</summary>
    NextDay = 3,
    /// <summary>Ekonomik (agir/buyuk yuk, daha yavas).</summary>
    Economy = 4
}

/// <summary>
/// Provider × tier → (metadata key, value, display) mapping katalogu.
/// Studio bir gonderi olusturulurken UI'dan gelen tier'i provider'in
/// kendi metadata anahtar/degerine ceviren tek noktadir.
/// </summary>
public static class ServiceTypeCatalog
{
    public sealed record ServiceTierMapping(
        CargoProviderTypeDto Provider,
        KargoyeriServiceTier Tier,
        string MetadataKey,
        string MetadataValue,
        string DisplayName,
        bool IsDefault = false);

    /// <summary>
    /// Provider × tier eslestirme tablosu.
    /// Provider DLL'leri zaten bu metadata key'leri okuyor; biz sadece UI'dan gelen tier'i
    /// dogru anahtar/degere ceviriyoruz.
    /// </summary>
    public static readonly IReadOnlyList<ServiceTierMapping> Mappings = new[]
    {
        // ── UPS ───────────────────────────────────────────────────────
        new ServiceTierMapping(CargoProviderTypeDto.Ups, KargoyeriServiceTier.Standard, "ups.serviceCode", "11", "UPS Standard", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.Ups, KargoyeriServiceTier.Express,  "ups.serviceCode", "07", "UPS Express"),
        new ServiceTierMapping(CargoProviderTypeDto.Ups, KargoyeriServiceTier.NextDay,  "ups.serviceCode", "65", "UPS Express Saver"),
        new ServiceTierMapping(CargoProviderTypeDto.Ups, KargoyeriServiceTier.Economy,  "ups.serviceCode", "08", "UPS Expedited"),

        // ── HepsiJet ──────────────────────────────────────────────────
        new ServiceTierMapping(CargoProviderTypeDto.HepsiJet, KargoyeriServiceTier.Standard, "hepsijet.deliveryType", "STANDARD", "Standart", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.HepsiJet, KargoyeriServiceTier.SameDay,  "hepsijet.deliveryType", "SAME_DAY", "Ayni Gun"),
        new ServiceTierMapping(CargoProviderTypeDto.HepsiJet, KargoyeriServiceTier.NextDay,  "hepsijet.deliveryType", "NEXT_DAY", "Ertesi Gun"),

        // ── PTT ───────────────────────────────────────────────────────
        new ServiceTierMapping(CargoProviderTypeDto.Ptt, KargoyeriServiceTier.Standard, "ptt.gonderiTip", "NORMAL", "PTT Normal", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.Ptt, KargoyeriServiceTier.Express,  "ptt.gonderiTip", "ACELE",  "PTT Acele"),
        new ServiceTierMapping(CargoProviderTypeDto.Ptt, KargoyeriServiceTier.NextDay,  "ptt.gonderiTip", "GECE",   "PTT Gece (Ertesi Gun)"),

        // ── Aras / MNG / Surat / Yurtici ──────────────────────────────
        // Bu sagiyicilarda servis tipi su an provider tarafindan ozel olarak okunmuyor;
        // metadata'ya yine de yazilir, ileride provider entegrasyonu eklenince otomatik aktive olur.
        new ServiceTierMapping(CargoProviderTypeDto.Aras, KargoyeriServiceTier.Standard, "aras.serviceCode", "STD", "Aras Standart", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.Aras, KargoyeriServiceTier.Express,  "aras.serviceCode", "EXP", "Aras Hizli"),

        new ServiceTierMapping(CargoProviderTypeDto.Mng, KargoyeriServiceTier.Standard, "mng.serviceCode", "STD", "MNG Standart", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.Mng, KargoyeriServiceTier.Express,  "mng.serviceCode", "EXP", "MNG Hizli"),

        new ServiceTierMapping(CargoProviderTypeDto.Surat, KargoyeriServiceTier.Standard, "surat.serviceCode", "STD", "Surat Standart", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.Surat, KargoyeriServiceTier.Express,  "surat.serviceCode", "EXP", "Surat Hizli"),

        new ServiceTierMapping(CargoProviderTypeDto.Yurtici, KargoyeriServiceTier.Standard, "yurtici.serviceCode", "STD", "Yurtici Standart", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.Yurtici, KargoyeriServiceTier.Express,  "yurtici.serviceCode", "EXP", "Yurtici Hizli"),

        // TrendyolExpress / Sandbox / Custom — tier ayrimi yok; default tek satir
        new ServiceTierMapping(CargoProviderTypeDto.TrendyolExpress, KargoyeriServiceTier.Standard, "trendyol.serviceTier", "STANDARD", "TY Express Standart", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.Sandbox, KargoyeriServiceTier.Standard, "sandbox.serviceTier", "STANDARD", "Sandbox", IsDefault: true),
        new ServiceTierMapping(CargoProviderTypeDto.Custom,  KargoyeriServiceTier.Standard, "custom.serviceTier",  "STANDARD", "Custom",  IsDefault: true),
    };

    /// <summary>Bir provider icin desteklenen tier'lari dondurur.</summary>
    public static IReadOnlyList<ServiceTierMapping> GetTiersFor(CargoProviderTypeDto provider) =>
        Mappings.Where(m => m.Provider == provider).ToArray();

    /// <summary>Provider+tier kombinasyonu icin mapping; eslesme yoksa null.</summary>
    public static ServiceTierMapping? Resolve(CargoProviderTypeDto provider, KargoyeriServiceTier tier) =>
        Mappings.FirstOrDefault(m => m.Provider == provider && m.Tier == tier);

    /// <summary>Provider icin default tier mapping; her provider icin Standard tanimlidir.</summary>
    public static ServiceTierMapping GetDefault(CargoProviderTypeDto provider) =>
        Mappings.FirstOrDefault(m => m.Provider == provider && m.IsDefault)
            ?? Mappings.First(m => m.Provider == provider);

    /// <summary>
    /// Provider+tier'a karsilik gelen metadata key/value'yu metadata sozlugune yazar.
    /// Eslesme yoksa hicbir sey yapmaz (no-op) — varsayilan provider davranisi devreye girer.
    /// </summary>
    public static void Apply(
        Dictionary<string, string> metadata,
        CargoProviderTypeDto provider,
        KargoyeriServiceTier tier)
    {
        var mapping = Resolve(provider, tier);
        if (mapping is null) return;
        metadata[mapping.MetadataKey] = mapping.MetadataValue;
        // Standartlasmis anahtari da yaz — rapor/tracking'de tek bir alana bakilabilsin
        metadata["kargoyeri.serviceTier"] = tier.ToString();
    }

    /// <summary>
    /// Metadata icindeki "kargoyeri.serviceTier" anahtarindan tier'i parse eder.
    /// Yoksa Standard.
    /// </summary>
    public static KargoyeriServiceTier ParseFromMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null) return KargoyeriServiceTier.Standard;
        if (!metadata.TryGetValue("kargoyeri.serviceTier", out var raw)) return KargoyeriServiceTier.Standard;
        return Enum.TryParse<KargoyeriServiceTier>(raw, true, out var t) ? t : KargoyeriServiceTier.Standard;
    }

    /// <summary>UI'da tier kullanici dostu Turkce ad.</summary>
    public static string GetDisplayName(KargoyeriServiceTier tier) => tier switch
    {
        KargoyeriServiceTier.Standard => "Standart",
        KargoyeriServiceTier.Express  => "Ekspres",
        KargoyeriServiceTier.SameDay  => "Ayni Gun",
        KargoyeriServiceTier.NextDay  => "Ertesi Gun",
        KargoyeriServiceTier.Economy  => "Ekonomik",
        _ => tier.ToString()
    };
}
