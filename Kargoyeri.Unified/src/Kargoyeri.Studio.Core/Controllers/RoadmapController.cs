using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

public sealed class RoadmapController : Controller
{
    private readonly ProviderCatalogService _providerCatalogService;

    public RoadmapController(ProviderCatalogService providerCatalogService)
    {
        _providerCatalogService = providerCatalogService;
    }

    public IActionResult Index()
    {
        return View(new RoadmapViewModel
        {
            CurrentFocus = "Faz 1 teslimini canli provider ve daha guclu onboarding ile kapatmak.",
            HostModes =
            [
                "Standalone panel olarak calisan web host",
                "Pazaryeri icine gomulebilen embedded host",
                "Istenirse harici kullanim icin API host"
            ],
            ArchitectureLayers =
            [
                "Domain: kargo entity, durum ve is dili",
                "Application: use-case servisleri ve orkestrasyon",
                "Infrastructure: provider, persistence, bildirim, log",
                "Hosts: web panel, embedded mod, API kabugu"
            ],
            Phases =
            [
                new()
                {
                    Phase = "Faz 1",
                    Title = "Calisan Standalone Urun",
                    Description = "Ayar, gonderi, log, worker ve yonetim ekranlarina sahip ana urun.",
                    Highlights = new[] { "Tek provider ile canli akis", "Provider ayar ekranlari", "Shipment listesi", "Bildirim ve loglar" },
                    Status = "Aktif",
                    Outcome = "Bagimsiz satilabilecek urun paneli",
                    Deliverables = new[] { "Dashboard ve checklist", "Provider konfigurasyonu", "Shipment detay aksiyonlari", "Bildirim takibi" },
                    NextSteps = new[] { "Ilk canli kargo firmasi", "Daha net dokumantasyon", "Musteri ilk kurulum akisi" }
                },
                new()
                {
                    Phase = "Faz 2",
                    Title = "Operasyonel Guclendirme",
                    Description = "Canli provider sayisi artar, timeout/retry ve operasyonel gorunurluk oturur.",
                    Highlights = new[] { "MNG ve UPS modern transport", "Rapor ve filtreleme", "Daha guclu bildirim", "Gercek DB" },
                    Status = "Bekliyor",
                    Outcome = "Operasyon ekibinin gundelik yukunu tasiyan saglam platform",
                    Deliverables = new[] { "Gercek DB", "Retry / timeout", "Canli transport", "Rapor ekranlari" },
                    NextSteps = new[] { "MNG", "UPS", "Queue ve raporlama" }
                },
                new()
                {
                    Phase = "Faz 3",
                    Title = "Embedded Dagitim",
                    Description = "Ayni urun pazaryeri icinde sag menude sayfa gibi calisir hale gelir.",
                    Highlights = new[] { "Embed host", "Menu ve tema uyumu", "Ayni core ile calisma", "Ayrica standalone satis" },
                    Status = "Planlandi",
                    Outcome = "Tek urunle iki farkli dagitim modeli",
                    Deliverables = new[] { "Embedded shell", "Tema entegrasyonu", "Host menu baglantisi", "Dagitim secenekleri" },
                    NextSteps = new[] { "Pazaryeri host", "Yetki uyumu", "Paketleme" }
                }
            ],
            DeliveryChecklist =
            [
                new()
                {
                    Title = "Standalone panel hazir",
                    Description = "Yeni urun arayuzu, dashboard ve operasyon ekranlari tamamlandi.",
                    IsDone = true,
                    ActionText = "Dashboard",
                    ActionUrl = Url.Action("Index", "Home") ?? "/"
                },
                new()
                {
                    Title = "Ilk canli provider bagla",
                    Description = "Preview degil gercek tasima akisi icin ilk firma entegre edilmeli.",
                    IsDone = false,
                    ActionText = "Provider ekranina git",
                    ActionUrl = Url.Action("Index", "Providers") ?? "/Providers"
                },
                new()
                {
                    Title = "Operasyonel saglamlik",
                    Description = "Gercek DB, retry, queue ve timeout katmani tamamlanmali.",
                    IsDone = false,
                    ActionText = "Roadmap'i oku",
                    ActionUrl = Url.Action("Index", "Roadmap") ?? "/Roadmap"
                },
                new()
                {
                    Title = "Embedded dagitim",
                    Description = "Pazaryeri hostuna gomulen mod ve menu uyumu final adim olacak.",
                    IsDone = false,
                    ActionText = "Embedded vizyonunu incele",
                    ActionUrl = Url.Action("Index", "Roadmap") ?? "/Roadmap"
                }
            ],
            ProviderVisuals = _providerCatalogService.List()
                .Select(StudioVisualRegistry.Resolve)
                .ToArray()
        });
    }
}
