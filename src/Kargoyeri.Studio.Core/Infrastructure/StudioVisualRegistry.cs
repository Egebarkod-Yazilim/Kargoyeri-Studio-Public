using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Models;

namespace Kargoyeri.Studio.Core.Infrastructure;

public static class StudioVisualRegistry
{
    public static ProviderVisualViewModel ResolveByProvider(CargoProviderTypeDto provider)
    {
        var code = provider.ToString().ToLowerInvariant();
        var stub = new ProviderProfileDto { ProviderCode = code, Provider = provider.ToString(), SupportedOperations = new List<string>(), SettingsSchema = new List<ProviderSettingFieldDto>() };
        return Resolve(stub);
    }

    public static ProviderVisualViewModel Resolve(ProviderProfileDto profile)
    {
        var key = profile.ProviderCode.ToLowerInvariant();

        return key switch
        {
            "aras" => Build(profile, "provider-theme--aras", "Kurumsal entegrasyon akisini hizli ac.", "Ayar, durum ve takip ekranlari ayni yapida tutulur.", "Aras resmi kaynak"),
            "yurtici" => Build(profile, "provider-theme--yurtici", "Siparis aktarimi ve durum takibi icin net ekran.", "Klasik kargo akisini sade bir panel diliyle toplar.", "Yurtici resmi kaynak"),
            "ups" => Build(profile, "provider-theme--ups", "Modern API ve hesap bazli kurulum akisi.", "Ozellikle hesap ve auth bilgilerini net alanlarla ayirir.", "UPS resmi kaynak"),
            "mng" => Build(profile, "provider-theme--mng", "Portal ve sorgu urunlerine uygun kurulum akisi.", "Toplu sorgu ve durum yenileme mantigina uygun sade alanlar sunar.", "MNG resmi kaynak"),
            "surat" => Build(profile, "provider-theme--surat", "Firma bilgilerini gir, durumu tek yerden yonet.", "Sade ekran yapisi ile hizli kurulum hedeflenir.", "Surat resmi kaynak"),
            "ptt" => Build(profile, "provider-theme--ptt", "Kamu odakli servisler icin stabil takip akisi.", "Barcode ve servis bilgileri icin kontrollu ilerleme sunar.", "PTT resmi kaynak"),
            "hepsijet" => Build(profile, "provider-theme--hepsijet", "Delivery API odakli HepsiJET kurulumu.", "Siparis ve teslimat akisini kaynak sistemle birlikte takip eder.", "HepsiJET resmi kaynak"),
            "trendyolexpress" => Build(profile, "provider-theme--trendyolexpress", "Paket ve depo akisini Trendyol tarafina uyumlu yonet.", "Klasik kargo formundan cok paket senkron mantigina odaklanir.", "Trendyol resmi kaynak"),
            _ => Build(profile, "provider-theme--sandbox", "Canliya gecmeden tum akisi prova et.", "Gercek firma bilgileri gelmeden ekran ve entegrasyonu test etmek icin kullanilir.", "Kargoyeri sandbox")
        };
    }

    private static ProviderVisualViewModel Build(
        ProviderProfileDto profile,
        string accentClass,
        string headline,
        string caption,
        string sourceLabel)
    {
        return new ProviderVisualViewModel
        {
            ProviderCode = profile.ProviderCode,
            ProviderName = profile.Provider,
            AccentClass = accentClass,
            Headline = headline,
            Caption = caption,
            ImageUrl = null,
            SourceLabel = sourceLabel,
            SourceUrl = profile.SourceUrl
        };
    }
}
