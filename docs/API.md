# Kargoyeri Studio API v1

Public B2B REST API. Tum endpoint'ler `application/json`.

## Authentication

Her istekte `X-Api-Key` header'i zorunludur. API anahtari tenant icin admin tarafindan
uretilir (`/admin/customers/{tenant}/api-keys`).

```http
GET /api/v1/shipments
X-Api-Key: studio-demo-key
```

## Base URL

| Environment | URL                              |
|-------------|----------------------------------|
| Production  | https://studio.kargoyeri.com     |
| Staging     | https://studio-staging.kargoyeri.com |
| Local       | http://localhost:5000            |

## Rate Limiting

Tenant basina **1000 request/dakika** (token-bucket). Asildiginda HTTP `429 Too Many Requests`
+ `Retry-After: 60` header'i doner.

## Idempotency

`POST /api/v1/shipments` icin `Idempotency-Key` header'i tavsiye edilir. Ayni key ile yapilan
ikinci cagri ayni response'u doner — gonderi cogaltilmaz. Key formati: UUID v4.

## Endpoints

### Shipments

| Method | Path                                       | Aciklama                       |
|--------|--------------------------------------------|--------------------------------|
| GET    | `/api/v1/shipments`                        | Liste (filtre + sayfalama)     |
| POST   | `/api/v1/shipments`                        | Yeni gonderi olustur           |
| GET    | `/api/v1/shipments/{shipmentReference}`    | Detay                          |
| POST   | `/api/v1/shipments/{shipmentReference}/refresh` | Provider'dan statu yenile |
| POST   | `/api/v1/shipments/{shipmentReference}/cancel`  | Iptal                     |
| GET    | `/api/v1/shipments/{shipmentReference}/logs`    | Operasyon log gecmisi     |

### Providers

| Method | Path                          | Aciklama                  |
|--------|-------------------------------|---------------------------|
| GET    | `/api/v1/providers`           | Tum provider katalog        |
| GET    | `/api/v1/providers/status`    | Tenant icin provider sagligi |

### Webhooks

`POST /api/v1/webhooks/callback` — provider'dan gelen statu update'leri.
HMAC-SHA256 signature `X-Webhook-Signature` header'inda.

### Health

| Method | Path                | Auth gerekli? |
|--------|---------------------|---------------|
| GET    | `/healthz`          | Hayir         |
| GET    | `/healthz/regional` | Hayir (P4)    |

## Statu Kodlari

| HTTP | Anlam                                   |
|------|-----------------------------------------|
| 200  | OK — basarili                           |
| 201  | Created — yeni kaynak olusturuldu       |
| 400  | Validation error (`{ errors: [...] }`)  |
| 401  | X-Api-Key eksik veya gecersiz           |
| 403  | API key tenant'i ile kaynak eslemiyor   |
| 404  | Kaynak bulunamadi                       |
| 409  | Idempotency conflict / state conflict   |
| 422  | Provider FAIL (response body'de detay)  |
| 429  | Rate limit                              |
| 500  | Internal — Sentry'de detay              |

## Error Format

Validation:
```json
{
  "type": "https://kargoyeri.com/errors/validation",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Recipient.Phone": ["Gecerli bir telefon numarasi giriniz."]
  }
}
```

Domain hata:
```json
{
  "type": "https://kargoyeri.com/errors/provider",
  "title": "Provider operasyonu basarisiz",
  "status": 422,
  "detail": "Aras: Customer code yanlis (E-1042)",
  "providerCode": "Aras",
  "providerErrorCode": "E-1042"
}
```

## Webhook Imzalama

Cagriya `X-Webhook-Signature: <hmac-hex>` ekle. Imza:
```
HMAC-SHA256(body, tenantWebhookSecret)
```
Server tarafinda asagidaki ornek dogrulamayi yapar (timing-safe):
```csharp
using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
var expected = Convert.ToHexString(hash).ToLowerInvariant();
return CryptographicOperations.FixedTimeEquals(
    Encoding.UTF8.GetBytes(expected),
    Encoding.UTF8.GetBytes(provided));
```

## OpenAPI

OpenAPI 3.0 JSON: `/swagger/v1/swagger.json`
Swagger UI: `/swagger`

## Postman Collection

Repo: `postman/Kargoyeri.Studio.postman_collection.json`

Kullanim:
1. Postman'i ac -> Import -> Upload Files
2. `baseUrl` ve `apiKey` collection variable'larini ayarla
3. Request'leri sirayla calistirabilirsin

## SDK

Resmi SDK su an icin yok. Postman collection'u kendi tercih ettigin dile (TypeScript/Python/PHP)
otomatik clientlik uretmek icin OpenAPI Generator ile beraber kullanabilirsin:

```bash
openapi-generator-cli generate \
  -i http://localhost:5000/swagger/v1/swagger.json \
  -g typescript-fetch -o ./client-ts
```

## Roadmap

- [ ] gRPC alternatifi (P6)
- [ ] GraphQL (P6 — degerlendirme)
- [ ] Webhook subscription API (P5-#7+)
- [ ] Rate limit per-endpoint (P6)
