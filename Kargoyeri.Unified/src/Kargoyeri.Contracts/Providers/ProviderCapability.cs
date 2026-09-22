namespace Kargoyeri.Contracts.Providers;

/// <summary>
/// Her kargo sağlayıcısının hangi işlemleri desteklediğini tanımlar.
/// UI bu enum'a göre şekillenir — desteklenmeyen işlemler gösterilmez.
/// </summary>
[Flags]
public enum ProviderCapability
{
    None             = 0,
    CreateShipment   = 1 << 0,  // Gönderi oluşturma
    TrackShipment    = 1 << 1,  // Kargo takibi
    CancelShipment   = 1 << 2,  // İptal
    PrintBarcode     = 1 << 3,  // Barkod / etiket
    GetRates         = 1 << 4,  // Fiyat sorgulama
    ValidateAddress  = 1 << 5,  // Adres doğrulama
    SchedulePickup   = 1 << 6,  // Kurye çağırma
    ListShipments    = 1 << 7,  // Gönderi listesi (provider tarafından)

    /// Standart bir sağlayıcının en yaygın özellikleri
    Standard = CreateShipment | TrackShipment | PrintBarcode,

    /// Tam özellikli sağlayıcı
    Full = Standard | CancelShipment | GetRates | ValidateAddress | SchedulePickup | ListShipments,
}

public static class ProviderCapabilityExtensions
{
    public static bool Has(this ProviderCapability cap, ProviderCapability flag) =>
        (cap & flag) == flag;

    public static string Label(this ProviderCapability cap) => cap switch
    {
        ProviderCapability.CreateShipment  => "Gönderi Oluştur",
        ProviderCapability.TrackShipment   => "Takip Et",
        ProviderCapability.CancelShipment  => "İptal Et",
        ProviderCapability.PrintBarcode    => "Barkod / Etiket",
        ProviderCapability.GetRates        => "Fiyat Sorgula",
        ProviderCapability.ValidateAddress => "Adres Doğrula",
        ProviderCapability.SchedulePickup  => "Kurye Çağır",
        ProviderCapability.ListShipments   => "Gönderi Listesi",
        _                                  => cap.ToString()
    };

    public static string Icon(this ProviderCapability cap) => cap switch
    {
        ProviderCapability.CreateShipment  => "📦",
        ProviderCapability.TrackShipment   => "📍",
        ProviderCapability.CancelShipment  => "✕",
        ProviderCapability.PrintBarcode    => "🏷",
        ProviderCapability.GetRates        => "💰",
        ProviderCapability.ValidateAddress => "📮",
        ProviderCapability.SchedulePickup  => "🚚",
        ProviderCapability.ListShipments   => "📋",
        _                                  => "•"
    };

    /// Tek tek bayrakları döner (None ve bileşik değerler hariç)
    public static IEnumerable<ProviderCapability> GetFlags(this ProviderCapability cap)
    {
        foreach (ProviderCapability flag in Enum.GetValues<ProviderCapability>())
        {
            if (flag == ProviderCapability.None) continue;
            if (!IsSingleBit(flag)) continue;
            if (cap.Has(flag)) yield return flag;
        }
    }

    private static bool IsSingleBit(ProviderCapability v) =>
        v != 0 && ((int)v & ((int)v - 1)) == 0;
}
