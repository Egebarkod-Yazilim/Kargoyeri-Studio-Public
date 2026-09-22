# Load Testing (P5-#9)

Kargoyeri Studio yuk testleri **k6** ile yazildi. Sonuclar **InfluxDB**'ye stream edilip
**Grafana** dashboard'unda gorsellesir.

## Test senaryolari

| Script        | Amac                                  | Sure  | Profil        |
|---------------|---------------------------------------|-------|----------------|
| `smoke.js`    | Sanity check (CI'da koşar)            | 30s   | 1 VU           |
| `baseline.js` | Production trafigi simulasyonu        | 17m   | 50 VU steady   |
| `stress.js`   | Sistem kirilma noktasi                | 33m   | 0 -> 500 VU    |
| `spike.js`    | Ani trafik sicrasi (Black Friday)     | 8m    | 10 -> 500 -> 10 |

## Hizli baslangic

```bash
# Lokal (kurulu k6 ile):
k6 run loadtest/smoke.js

# Staging:
k6 run -e BASE_URL=https://studio-staging.kargoyeri.com -e API_KEY=$STAGING_KEY loadtest/baseline.js
```

## Docker stack (k6 + Grafana + InfluxDB)

```bash
cd loadtest
docker compose up -d              # InfluxDB + Grafana ayaga kalk
docker compose run --rm k6 run \
    -e BASE_URL=http://host.docker.internal:5000 \
    -e API_KEY=studio-demo-key \
    /scripts/baseline.js

# Grafana: http://localhost:3000 (anonymous Admin)
# Dashboard: k6/Kargoyeri Studio - k6 Load Test
```

## Threshold'lar (kabul kriterleri)

### baseline.js (50 VU steady)
- `http_req_failed` < %2
- `http_req_duration` p95 < 800ms, p99 < 2s
- `list-shipments` p95 < 400ms
- `create-shipment` p95 < 1.5s

### stress.js (0 -> 500 VU)
- 500 VU altinda hata < %10 (rate limit + circuit breaker bunu sagliyor olmali)
- p95 < 5s (bu noktada degradation acceptable)

### spike.js
- Spike sonrasi 1 dakika icinde sistem normale donsun
- Spike anindaki hatalar < %15

## CI/CD entegrasyon

`.github/workflows/loadtest.yml` ornek:

```yaml
- name: k6 smoke
  uses: grafana/k6-action@v0.3.1
  with:
    filename: loadtest/smoke.js
  env:
    BASE_URL: https://studio-staging.kargoyeri.com
    API_KEY: ${{ secrets.STAGING_API_KEY }}
```

## Sonuc analizi

Failure pattern'leri:
- **High p99 + low p50**: GC pause veya DB connection pool exhaustion
- **Sustained 5xx > %5**: Backing service down (DB/Redis/provider)
- **Sustained 429**: Rate limit tetiklendi (intended ama tenant ayarini gozden gecir)
- **Timeouts artiyor (TPS dustugu halde)**: Kuyruklarda backup, scale out gerekli

## Production load test izni

Production'da load test calistirmadan once:
- [ ] Off-peak saatte (gece 02:00-04:00 TR saati)
- [ ] On-call ekibe haber ver
- [ ] Sentry alert'leri gecici sustur
- [ ] Test tenant kullan, gercek tenant trafigine etki etmesin
- [ ] DB iops ve connection pool monitor edilsin
