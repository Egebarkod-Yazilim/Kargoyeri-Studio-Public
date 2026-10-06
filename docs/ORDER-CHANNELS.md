# Order Channels — Multi-Source Sipariş Girişi

Kargoyeri Studio sipariş girişlerini **4 farklı kaynaktan** alır. Müşteri herhangi birini, ikisini veya hepsini birden açabilir; bunlar bağımsız çalışır ve aynı kargo entegrasyon hattını paylaşır.

## Kaynaklar

| #  | Kaynak | Tip | Aktivasyon |
|----|--------|-----|------------|
| 0  | **Manuel sipariş girişi** | Studio içi form | Her zaman aktif |
| 1  | **Pazaryeri API'leri** | Hepsiburada, Trendyol, N11, Çiçek Sepeti, GittiGidiyor, Pazarama, Amazon TR | Müşteri kayıt sırasında / sonrasında credential girişi |
| 2  | **NopCommerce** | Müşterinin kendi NopCommerce instance'ı | OAuth client + base URL |
| 3  | **Gömülü Mini-Shop** | Studio içine gömülü hafif mağaza | UI hazır, backend `ComingSoon` |

Müşteri **Sipariş Kaynakları** sayfasından (`/admin/order-channels`) her kanal için bağımsız:
- Aktif/pasif toggle
- Credential girişi (per-channel, secret alanlar maskelenir)
- Varsayılan kargo firması seçimi
- Bağlantı testi
- "Şimdi senkronize et" butonu

yapabilir.

## Mimari

```
src/Kargoyeri.Studio.Core/Infrastructure/OrderChannels/
├── OrderChannelType.cs            # enum (Hepsiburada, Trendyol, ..., NopCommerce, EmbeddedShop)
├── OrderChannelDescriptor.cs      # Catalog: her channel'in alan şeması (Field[])
├── IOrderChannelAdapter.cs        # TestConnection + FetchOrders contract
├── OrderChannelMetadata.cs        # CustomerProfile.Metadata read/write (channel.{code}.{field})
├── OrderChannelRegistry.cs        # DI'a kayıtlı adapter'ları type'a göre arar
├── OrderChannelSyncEngine.cs      # Orkestratör + Run/Monitor
└── Adapters/
    ├── SimulationOrderChannelAdapterBase.cs   # 9 channel'in skeleton implementasyonu (fallback)
    ├── HepsiburadaLiveAdapter.cs              # CANLI — REST + Basic Auth
    ├── TrendyolLiveAdapter.cs                 # CANLI — REST + Basic Auth
    └── N11LiveAdapter.cs                      # CANLI — SOAP envelope
```

```
Controllers/OrderChannelsController.cs   # Index + Configure(channel) per-marketplace sayfası
Views/OrderChannels/Index.cshtml          # Tüm kanallar grid
Views/OrderChannels/Configure.cshtml      # Tek pazaryerine adanmış detay sayfası
```

## Canlı Adapter'lar (Live)

Üç büyük Türk pazaryeri için gerçek API entegrasyonu mevcuttur. Diğerleri `Simulation` aşamasındadır ve şu anda örnek sipariş üretir; canlı API'leri eklenince aynı `IOrderChannelAdapter` kontratıyla devreye girecektir.

### Hepsiburada (`HepsiburadaLiveAdapter`)
- **Endpoint:** `GET https://oms-external.hepsiburada.com/packages/merchantid/{merchantId}`
- **Auth:** Basic `base64(username:password)`
- **User-Agent (zorunlu):** `{merchantId} - SelfIntegration`
- **Tarih:** ISO 8601 (`begindate`/`enddate`)
- **Concept:** Hepsiburada siparişleri "package" olarak döner; `packageNumber` → `ChannelExternalOrderId`.
- **HttpClient ismi:** `hepsiburada-orders`

### Trendyol (`TrendyolLiveAdapter`)
- **Endpoint:** `GET https://apigw.trendyol.com/integration/order/sellers/{supplierId}/orders`
- **Auth:** Basic `base64(apiKey:apiSecret)`
- **User-Agent (zorunlu):** `{supplierId} - SelfIntegration`
- **Tarih:** Unix epoch ms (`startDate`/`endDate`); maksimum 14 günlük pencere
- **HttpClient ismi:** `trendyol-orders`

### N11 (`N11LiveAdapter`)
- **Endpoint:** `POST https://api.n11.com/ws/OrderService.wsdl` (SOAP)
- **Auth:** SOAP envelope içinde `<auth><appKey>...</appKey><appSecret>...</appSecret></auth>`
- **Operation:** `OrderListRequest` (status: `New`+`Approved` paralel çekilir)
- **Rate:** 1000 req/dk
- **HttpClient ismi:** `n11-orders`

DI sırası önemlidir: `OrderChannelRegistry` aynı `OrderChannelType` için `First()` adapter'ı seçer. Live adapter'lar Simulation versiyonlarından **önce** kayıtlıdır (`StudioServiceExtensions.cs`).

### Strategy pattern

Her kanal için bir `IOrderChannelAdapter` implementasyonu kayıtlıdır. `OrderChannelRegistry` DI'dan tüm adapter'ları toplar, `OrderChannelType`'a göre indeksler. Yeni bir kanal eklemek için:

1. `OrderChannelType` enum'a yeni değer ekle
2. `OrderChannelCatalog._all` dizisine descriptor ekle (DisplayName, Group, Field listesi)
3. Yeni bir `IOrderChannelAdapter` implement et (basitse `SimulationOrderChannelAdapterBase` türet)
4. `StudioServiceExtensions.cs` içine `services.AddSingleton<IOrderChannelAdapter, MyAdapter>()` ekle

### Metadata storage

Her kanalın bilgisi `CustomerProfile.Metadata` dictionary'sine `channel.{code}.{field}` namespace'iyle yazılır:

```
channel.hepsiburada.enabled         = true
channel.hepsiburada.merchantId      = 123456
channel.hepsiburada.username        = api_user
channel.hepsiburada.password        = (encrypted)
channel.hepsiburada.defaultProvider = aras
channel.hepsiburada.lastSyncUtc     = 2026-05-06T10:00:00Z

channel.trendyol.enabled            = true
channel.trendyol.supplierId         = 987654
channel.trendyol.apiKey             = (encrypted)
channel.trendyol.apiSecret          = (encrypted)
```

Bu sayede aynı müşteri birden fazla pazaryeri + NopCommerce kombinasyonunu eş zamanlı tutabilir; her birinin lastSync'i ayrı izlenir.

## Stage'ler

| Stage         | Anlam |
|---------------|-------|
| `Live`        | Production'a hazır, gerçek API çağırır |
| `Simulation`  | Adapter şeması var, deterministik sahte sipariş üretir (test için) |
| `ComingSoon`  | UI hazır ama aktif değil (örn. EmbeddedShop bu sürümde) |

Bu sürümde **8 kanal Simulation modunda**, **EmbeddedShop ComingSoon**. Production'a alacağınız kanal için:

```csharp
// Adapters/HepsiburadaOrderChannelAdapter.cs
public sealed class HepsiburadaOrderChannelAdapter : SimulationOrderChannelAdapterBase
{
    public override OrderChannelType Type => OrderChannelType.Hepsiburada;
    // SimulationOrderChannelAdapterBase'i override et:
    public override async Task<OrderChannelTestResult> TestConnectionAsync(...) { /* gerçek API */ }
    public override async Task<OrderChannelFetchResult> FetchOrdersAsync(...) { /* gerçek API */ }
}
// Catalog descriptor'ında Stage = OrderChannelStage.Live yap.
```

## Sync akışı

```
[OrderChannelsController.Sync]
    ↓
OrderChannelSyncEngine.SyncChannelAsync(tenant, channel)
    ↓
OrderChannelMetadata.Read(profile, channel)  → state (Enabled, LastSyncUtc, Fields)
    ↓
OrderChannelRegistry.Get(channel)            → adapter
    ↓
adapter.FetchOrdersAsync(creds, since)       → RawIncomingOrder[]
    ↓
[TODO sonraki sürüm] → Shipment creation pipeline
    - ChannelExternalOrderId ile dedup
    - state.DefaultProvider ile kargo firması seç
    - WorkspaceFeatureMetadata sender bilgileriyle birleştir
    - ShipmentAddressValidationService + Anomaly detection
    - CargoShipment.Create(... IntegrationSourceType.Marketplace ...)
    ↓
OrderChannelSyncMonitor.Record(run)
```

`MarketplaceOrderSyncEngine` (önceki tek-marketplace yapısı) hâlâ çalışır durumda — geriye uyumluluk için bırakıldı. Yeni multi-channel sistem ona alternatif olarak **paralel** koşar; aynı tenant için her ikisi de aktif olabilir, aynı `IntegrationSourceType.Marketplace` flag'iyle yazar.

## Manuel giriş

Manuel sipariş girişi (`/shipments/create`) hiçbir channel ile koşullanmamıştır; her zaman aktiftir. Bu yüzden bir müşteri:
- Sadece manuel giriş yapabilir (channel hiç açmadan)
- Manuel + 1-N pazaryeri API
- Sadece NopCommerce + manuel
- Hepsi açık

şeklinde **istediği kombinasyonu** seçebilir.

## Roadmap

- [ ] Hepsiburada + Trendyol + N11 için `Live` adapter implementasyonu
- [ ] NopCommerce REST API (OAuth2 + Get Orders) implementasyonu
- [ ] Gömülü mini-shop projesi (`Kargoyeri.Studio.Shop`) — minimal Razor Pages mağaza
- [ ] Adapter'lardan `RawIncomingOrder` → `CargoShipment` mapper service
- [ ] Background sync (mevcut `MarketplaceOrderSyncService` paterniyle, multi-channel versiyonu)
- [ ] Webhook ingestion (channel'lar push ettiğinde)
