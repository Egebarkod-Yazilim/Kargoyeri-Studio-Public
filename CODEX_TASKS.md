# Kargoyeri.Studio — Codex Görev Paketi (Refactor: Clean Architecture)

> Bu dosya, `Kargoyeri.Studio.Clean` çözümünü **ETool.Next** kalitesinde katmanlı bir iş
> modülüne dönüştürme işinin **Codex'e ait yarısıdır**. Diğer yarısını (mimari omurga)
> Claude yürütüyor. Aşağıdaki "DOKUNMA" kurallarına uyman şart, yoksa çakışırız.

## 0. Ortam / yollar

- Hedef proje (Clean):   `C:\Users\Yazilim\Desktop\Kargoyeri.Studio`
- Kaynak-doğru sürüm:    `C:\Users\Yazilim\Desktop\Kargoyeri.Studio\Kargoyeri.Unified`  ← katman **kaynak kodu** burada, 0 hata ile derleniyor
- Kalite referansı:      `C:\Users\Yazilim\Desktop\Projeler\ETool\ETool.Next`  ← taklit edeceğimiz desen
- Derleyici:             `"C:\Program Files\dotnet\dotnet"` (SDK 9.0.300). Her görev sonunda `dotnet build` = **0 hata** olmalı.

## 1. Hedef mimari (ETool.Next deseni)

```
Kargoyeri.Domain          → Common, Entities, Enums                         (bağımsız, saf)
Kargoyeri.Application     → Abstractions, Cqrs, Features, Models, Services  (iş kuralları)
Kargoyeri.Infrastructure  → Persistence, Integrations, Security, Email      (dış dünya)
Kargoyeri.Api (Web)       → Controllers (ince), Program.cs, Workers         (sunum)
```

## 2. DOKUNMA — Claude'un sahiplendiği alanlar (SEN DÜZENLEME)

Bu dosya/klasörleri **değiştirme**; sadece okuyup örnek alabilirsin:
- `*.sln` (çözüm dosyası)
- `Kargoyeri.Domain/**`
- `Kargoyeri.Application/Abstractions/**` ve `Kargoyeri.Application/Cqrs/**`
- `Kargoyeri.Infrastructure/Persistence/**` (DbContext, EF config, migrations) ve `.../Security/**`
- `Kargoyeri.Api/Program.cs` ve DI kompozisyon kökü
- Şu controller'lar (Claude bölecek): `ShipmentsController`, `ShipmentsApiController`,
  `BulkShipmentController`, `TrackingController`, `OrderChannelsController`,
  `TenantSwitchController`, `WorkspacesController`, `AccessController`

> DI kaydı gereken her modül için **kendi** `AddXxx(IServiceCollection)` uzatma metodunu
> kendi klasörüne yaz ve listesini bana bırak; `Program.cs`'i sen düzenleme.

## 3. Codex görevleri

### PART 1 — Hemen başlayabilirsin (Claude'un iskeletine bağlı değil)

**G1. Temizlik / hijyen** (sadece üret/sil, kaynak koda dokunma)
- Sil: tüm `obj/`, `bin/`, `.vs/`, `.vs.stale-*`, `.vs.reset-*`, `build-web.log`,
  `build-web-norestore.log`, `studio-run.log`, `studio-run.err.log`.
- Kök dizine düzgün bir `.gitignore` (standart VisualStudio/.NET şablonu) ve `.editorconfig`
  (C# nullable, `file-scoped namespace`, 4 boşluk) ekle.
- `_recovery/` ve `Kargoyeri.Unified/` klasörlerine DOKUNMA (yedek + kaynak).

**G2. Entegrasyon katmanı refactor** — `Infrastructure/Integrations/`
Kaynak: `Kargoyeri.Unified/src/Kargoyeri.Infrastructure` + Studio.Core/Infrastructure.
Mevcut dağınık entegrasyonları ETool.Next'teki `Infrastructure/Integrations` düzenine göre
alt-klasörlere ayır, **mevcut arayüzleri (interface) koruyarak**:
- `Integrations/Cargo/<Provider>/` — her kargo sağlayıcı adaptörü kendi klasöründe
- `Integrations/Marketplace/<Channel>/` — pazaryeri/OrderChannel adaptörleri
- `Integrations/EInvoice/`, `Integrations/Payments/`, `Integrations/Storage/` — mevcut
  `EInvoice`, `Payments`, `Storage` alt-klasörlerini buraya taşı
- Ortak sözleşmeler `Application/Abstractions`'ta kalır (oraya YENİ dosya ekleme, Claude yönetiyor;
  eksik arayüz varsa bana not düş).
- Her entegrasyon grubuna `AddCargoIntegrations()`, `AddMarketplaceIntegrations()` vb. DI
  uzatma metodu yaz.

### PART 2 — Claude iskeleti (`Domain/Application/Infrastructure/Api`) hazır olduktan sonra

**G3. Düz controller'ları katmana ayır** (aşağıdakiler — God-class OLMAYANLAR).
Desen: **ince controller → `Application/Services/<Alan>Service`** (bunlarda tam CQRS gerekmez,
sadece iş mantığını controller'dan servise taşı, controller yalnızca çağırıp döndürsün).
Claude bir referans controller'ı bu desende bölecek; onu **birebir örnek alarak** şunları böl:

```
Providers, ProvidersApi, ProviderHealth, ProviderCertification, RegionalHealth,
Notifications, NotificationTemplates, Billing, Signup, Onboarding, ApiKeys,
WebhookCallback, WebhookDelivery, Audit, ActivityLog, Metrics, Reports, Pickup,
AccountData, Security, Culture, Legal, Roadmap, ApiDocs, Mobile, Embed, Worker,
Workflow, Home
```
Her alan için: `Application/Services/<Alan>/I<Alan>Service.cs` + `<Alan>Service.cs`,
controller sadece servise delege eder. Metot başına <~30 satır hedefle.

**G4. Model bölme** — `Models/StudioViewModels.cs` (~43KB tek dosya) alanlara göre böl:
`Application/Models/<Alan>/*.cs` (ör. `Shipments/ShipmentViewModel.cs`). Tek dosyada birden
çok tip bırakma; 1 tip = 1 dosya (küçük ilişkili DTO'lar gruplanabilir).

**G5. Namespace normalizasyonu** — taşınan her dosyanın `namespace`'ini yeni klasör yoluna
uyarla (`file-scoped namespace`). Kırılan `using`'leri düzelt, `dotnet build` = 0 hata.

## 4. Teslim kuralları
- Her görev sonunda: `dotnet build "Kargoyeri.Studio.Clean.sln" -c Release` → **0 hata**.
- Uyarıları (CS8601/CS8604 nullable) artırma; azaltırsan bonus.
- Yaptığın DI uzatma metotlarının listesini `CODEX_DONE.md`'ye yaz ki Claude `Program.cs`'e bağlasın.
- İş mantığını **değiştirme**, sadece **taşı/ayır/yeniden düzenle** — davranış birebir korunmalı.
