# Provider Sertifikasyon Matrix (P5-#6)

Her cargo saglayici icin canliya cikmadan once 4 testin gecmesi sart:

| Adim          | Test                                       | Beklenen           |
|---------------|---------------------------------------------|--------------------|
| auth-check    | Settings + endpoint config dogru mu?       | OK                 |
| create-shipment | Test gonderi olusturulabiliyor mu?       | TrackingNumber donüyor |
| refresh-status | Olusturulan gonderinin statusu okunabiliyor mu? | Status alani dolu |
| cancel-shipment | Test gonderi iptal edilebiliyor mu?     | Cancelled          |

## Calistirmak

Admin paneline /admin/cert ile gir, tenant + provider sec, "Sertifikasyonu calistir" tikla.
Sonuc gercek zamanli ekrana basilir + JSON file'a yazilir:
- Path: `<ContentRoot>/provider-cert/yyyy-MM-dd/{tenantKey}-{provider}-HHmmss.json`
- 7 gunluk gecmis arayuzde gosterilir

## Saglayici Bazli Credential Matrix

### Aras Kargo
| Field           | Tip        | Notlar                                       |
|-----------------|------------|----------------------------------------------|
| EndpointBase    | URL        | https://customerservices.araskargo.com.tr/   |
| Username        | string     | Aras tarafindan saglanir                     |
| Password        | secret     | -                                            |
| CustomerCode    | string     | Sozlesme musteri kodu                        |
| Branch          | string     | Sube kodu (opsiyonel)                        |

Sertifikasyon: SOAP `setOrder` -> `getOrder` -> `cancelOrder` flow.

### MNG Kargo
| Field           | Tip        |
|-----------------|------------|
| EndpointBase    | URL        |
| ApiKey          | secret     |
| CustomerNumber  | string     |
| Password        | secret     |

REST: `POST /orders` -> `GET /orders/{id}` -> `DELETE /orders/{id}`.

### Yurtici Kargo
| Field           | Tip        |
|-----------------|------------|
| EndpointBase    | URL        |
| Username        | string     |
| Password        | secret     |
| WsUserName      | string     |
| WsPassword      | secret     |

SOAP: `createShipment` -> `queryShipment` -> `cancelShipment`.

### PTT Kargo
| Field           | Tip        |
|-----------------|------------|
| EndpointBase    | URL        |
| Username        | string     |
| Password        | secret     |
| ContractNumber  | string     | Anlasma no                                   |

REST: token al -> `POST /shipment` -> `GET /shipment/{barcode}` -> `POST /cancel`.

### Surat Kargo
| Field           | Tip   |
|-----------------|-------|
| EndpointBase    | URL   |
| Username        | string |
| Password        | secret |
| CustomerCode    | string |

### Hepsijet
| Field           | Tip   |
|-----------------|-------|
| EndpointBase    | URL   |
| ApiKey          | secret |
| MerchantCode    | string |

### Trendyol Express
| Field           | Tip   |
|-----------------|-------|
| EndpointBase    | URL   |
| ApiKey          | secret |
| ApiSecret       | secret |
| SupplierId      | string |

### UPS
| Field           | Tip   |
|-----------------|-------|
| EndpointBase    | URL   |
| AccessKey       | secret |
| Username        | string |
| Password        | secret |
| AccountNumber   | string |

## Production Go-Live Checklist

Bir tenant'in canliya cikmasi icin:

- [ ] Tenant'a ait her aktif provider icin son 7 gun icinde **basarili** sertifikasyon kosusu var
- [ ] auth-check + create + refresh + cancel hepsi OK
- [ ] Provider response sureleri kabul edilebilir (p95 < 5 sn)
- [ ] Tenant'in test gonderileri provider sisteminde iptal/temizlenmis
- [ ] Sertifikasyon raporu tenant ile paylasildi (PDF/JSON)
- [ ] Webhook callback URL'leri provider tarafinda kayitli
- [ ] Rate limit kontrolu yapildi (provider'a saniyede max X istek)

## Sertifikasyon Sikinti Trasingi

### auth-check FAIL: "Provider settings bulunamadi"
- /admin/customers/{tenant}/providers altinda ilgili provider configi yok.

### create-shipment FAIL: "EXCEPTION: ..."
- HTTP 401 -> credentials yanlis
- HTTP 403 -> IP whitelist
- HTTP 500 -> provider tarafi (rapor cek + provider'i ara)
- Timeout -> network/proxy

### create OK ama refresh FAIL
- TrackingNumber provider'da hemen indekslenmemis olabilir, 5-10 sn sonra tekrar dene
- Provider asenkron olusturma yapiyor olabilir

### create+refresh OK ama cancel FAIL
- Sertifikasyon urunu artik provider'da fiziksel kargoya gitmis olabilir; manuel temizleme gerekebilir
- Bazi provider'lar status=InTransit oldugunda cancel'i reddeder

## Raporlama

Sonuc JSON formati:
```json
{
  "tenantKey": "studio-demo",
  "provider": "Aras",
  "startedAtUtc": "2026-05-05T08:30:12Z",
  "finishedAtUtc": "2026-05-05T08:30:18Z",
  "overallSuccess": true,
  "totalDurationMs": 5421,
  "steps": [
    { "code": "auth-check", "success": true, "durationMs": 0, "message": "Endpoint OK: https://..." },
    { "code": "create-shipment", "success": true, "durationMs": 1850, "message": "TrackingNumber=ARK20260505000001" },
    { "code": "refresh-status", "success": true, "durationMs": 920, "message": "Status=Pending" },
    { "code": "cancel-shipment", "success": true, "durationMs": 2651, "message": "Cancelled" }
  ]
}
```
