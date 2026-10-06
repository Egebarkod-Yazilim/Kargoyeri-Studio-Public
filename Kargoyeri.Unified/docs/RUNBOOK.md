# Kargoyeri Studio — Operasyon RUNBOOK

Bu doküman canlı ortamda günlük işletim, yedekleme, kurtarma ve sorun giderme adımlarını içerir.

---

## 1. Sistem Özeti

| Bileşen | Açıklama |
|---|---|
| **Studio.Web** | ASP.NET Core 8 MVC host (sidecar). Razor Class Library `Studio.Core`'u içerir. |
| **SQL Server** | Birincil persistans (Storage Mode = SqlServer). JSON modunda dosya kullanılır. |
| **Data Protection Keys** | `data-protection-keys/studio-web/` altında. Cookie & antiforgery şifreleme. |
| **Logs** | `logs/studio-YYYYMMDD.log` (rolling, 14 gün). Seq URL tanımlıysa Seq'e de gider. |
| **Storage (JSON modu)** | `Storage:BasePath` altında JSON dosyalar (`customers.json`, `shipments.json` vb.) |

---

## 2. Yedekleme Stratejisi (Backup)

### 2.1 SQL Server modu

**Günlük tam yedek (her gece 02:00 UTC):**

```powershell
# scripts\backup-sqlserver.ps1
$ts        = Get-Date -Format "yyyyMMdd-HHmmss"
$backupDir = "D:\Backups\Kargoyeri"
$db        = "KargoyeriStudio"
$file      = Join-Path $backupDir "$db-$ts.bak"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null

sqlcmd -S "." -E -Q "BACKUP DATABASE [$db] TO DISK='$file' WITH COMPRESSION, CHECKSUM, INIT;"

# 14 günden eski .bak dosyalarını sil
Get-ChildItem $backupDir -Filter "$db-*.bak" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-14) } |
    Remove-Item -Force
```

Windows Task Scheduler ile günlük tetikle: `pwsh -File scripts\backup-sqlserver.ps1`.

**Saatlik transaction-log yedek** (kurtarma noktası granülerliği için, FULL recovery model'de):

```sql
BACKUP LOG [KargoyeriStudio] TO DISK='D:\Backups\Kargoyeri\Logs\KS-$(yyyymmddHHmm).trn' WITH COMPRESSION;
```

### 2.2 JSON Storage modu

```powershell
# scripts\backup-json.ps1
$ts        = Get-Date -Format "yyyyMMdd-HHmmss"
$src       = "C:\Kargoyeri\Storage"          # Storage:BasePath
$dst       = "D:\Backups\Kargoyeri\json-$ts.zip"
Compress-Archive -Path "$src\*" -DestinationPath $dst -CompressionLevel Optimal

Get-ChildItem "D:\Backups\Kargoyeri" -Filter "json-*.zip" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item -Force
```

### 2.3 Data Protection anahtarları

Cookie ve antiforgery şifreleme anahtarları kaybolursa kullanıcılar logout olur ve mevcut form token'ları geçersizleşir. Yedeklenmesi **kritik**.

```powershell
# Studio.Web ContentRoot içindeki klasör
Compose-Archive -Path "C:\inetpub\Kargoyeri\data-protection-keys\*" `
                -DestinationPath "D:\Backups\Kargoyeri\dpk-$ts.zip"
```

### 2.4 Off-site kopya

Yedekleri haftalık olarak dış lokasyona (S3 / Azure Blob / başka sunucu) sync edin:

```powershell
aws s3 sync "D:\Backups\Kargoyeri" "s3://egebarkod-backups/kargoyeri/" --delete
```

### 2.5 Yedek doğrulama (aylık)

Yedek alındığını varsaymayın — geri yükleme tatbikatı yapın:

1. Test sunucusunda son `.bak` dosyasını `RESTORE DATABASE` ile yükleyin.
2. Studio.Web'i bu DB'ye bağlayıp `/health/ready` 200 döndüğünü, müşteri/gönderi sayısının beklenen olduğunu doğrulayın.
3. Sonucu `docs\backup-restore-log.md` içine düşün.

---

## 3. Geri Yükleme (Restore)

### 3.1 SQL Server'dan tam restore

```sql
USE master;
ALTER DATABASE [KargoyeriStudio] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [KargoyeriStudio]
   FROM DISK = 'D:\Backups\Kargoyeri\KargoyeriStudio-20260101-020000.bak'
   WITH REPLACE, RECOVERY;
ALTER DATABASE [KargoyeriStudio] SET MULTI_USER;
```

Studio.Web'i restart et: `Restart-Service KargoyeriStudio` (veya IIS app pool recycle).

### 3.2 JSON'dan restore

1. Studio.Web servisini durdur.
2. `Storage:BasePath` klasörünü yedeğe karşı zip'i extract et.
3. Servisi başlat. `/health/ready` kontrol et.

### 3.3 DPK kaybı sonrası

DPK silindiyse:
- Tüm aktif kullanıcı oturumları sonlanır → tekrar giriş gerekir.
- Antiforgery hataları kaybolması için tüm kullanıcılar tarayıcı cache'ini temizlemeli (`Ctrl+F5`).
- Yedekten dpk-*.zip extract → Studio.Web'i yeniden başlat.

### 3.4 Yedek doğrulama (P2-#8)

Yedek dosyalarının açılabilirliğini düzenli olarak teyit etmek için
`scripts/restore-verify.ps1` kullanılır. Üretimde **haftalık** çalıştırılması zorunlu.

**Hızlı mod** — sadece `RESTORE VERIFYONLY` (checksum + header):
```pwsh
pwsh -NoProfile -File scripts\restore-verify.ps1
```

**Tam mod** — geçici DB'ye gerçek restore + sentinel sorgu (önerilen, haftalık):
```pwsh
pwsh -NoProfile -File scripts\restore-verify.ps1 -FullRestore
```

Sonuç `D:\Backups\Kargoyeri\verify\last-verify.json` dosyasına yazılır:

```json
{
  "status": "OK",
  "message": "VERIFYONLY + full restore basarili",
  "backupFile": "D:\\Backups\\Kargoyeri\\KargoyeriStudio-20260427-020000.bak",
  "sizeMB": 142.7,
  "ageHours": 6.4,
  "sentinelRows": 38,
  "fullRestore": true
}
```

**Status değerleri:** `OK` (exit 0), `FAIL` (exit 1/2/4), `STALE` (exit 3 — en yeni yedek `MaxAgeHours`'tan eski).

**Task Scheduler — Pazar 03:30 haftalık:**
```pwsh
$action  = New-ScheduledTaskAction -Execute "pwsh.exe" `
           -Argument "-NoProfile -File C:\Kargoyeri\scripts\restore-verify.ps1 -FullRestore"
$trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 3:30am
Register-ScheduledTask -TaskName "Kargoyeri-RestoreVerify" -Action $action -Trigger $trigger `
    -RunLevel Highest -User "SYSTEM"
```

**İzleme:** `last-verify.json` içindeki `status != "OK"` veya `finishedAtUtc > 8 gün` ise alarm üret.

---

## 4. Sağlık Kontrolleri

| Endpoint | Beklenen | Sıklık |
|---|---|---|
| `GET /health/live` | 200, "Healthy" | 30 sn |
| `GET /health/ready` | 200, tüm "ready" check'ler Healthy | 30 sn |
| `GET /health` | Detaylı JSON (storage, tenant repo, vs.) | 5 dk |

UptimeRobot / Pingdom'a `/health/ready` ekleyin; 3 ardışık fail → SMS uyarısı.

---

## 5. Sık Karşılaşılan Senaryolar

### 5.1 Kullanıcı login olamıyor
1. `/health` → cookie store'da hata var mı?
2. `logs/studio-*.log` → "AntiforgeryValidationException" → DPK sorunu, bkz. 3.3.
3. Rate limit'e takıldı mı? IP başına 5 dk içinde 10 deneme → 1 dk bekle.

### 5.2 Provider çağrıları başarısız
1. `/swagger` üzerinden `POST /api/v1/shipments/{ref}/refresh` test et — provider mı, network mü hata veriyor?
2. `Operasyon Loglari` ekranı (Detail) → ProviderPayload → hata mesajı.
3. Provider credential'ları geçerli mi? `Providers` ekranında "Test Et" butonu.

### 5.3 Webhook geliyor ama statü güncellenmiyor
1. `/api/v1/webhooks/...` audit log'u → IP allowlist'e takıldı mı?
2. Tracking number eşleşmemiş olabilir — `WebhookCallbackController` log'larında "tracking not found" var mı?

### 5.4 Disk dolu
- `logs/` 14 gün retention'a sahip; manuel `Remove-Item logs\studio-2025*.log` ile temizleyin.
- `data-protection-keys/` rotasyon eski anahtarları silmez — 90 günden eski dosyalar manuel temizlenebilir (yedek aldıktan sonra).
- DB için: `BACKUP LOG ... WITH TRUNCATE_ONLY` (tehlikeli, son çare).

### 5.5 Iptal/Cancel butonu çalışmıyor
- `CancelPolicy.cs` listesini kontrol et (Delivered/Cancelled iptal edilemez).
- Provider PTT veya TrendyolExpress ise UI uyarı verir — bu doğru davranıştır.

---

## 6. Deploy / Release

```bash
# 1. Git pull master
git pull origin master

# 2. Build
dotnet publish src/Kargoyeri.Studio.Web -c Release -o publish/

# 3. Servisi durdur
Stop-Service KargoyeriStudio

# 4. Dosyaları kopyala (DPK ve appsettings.Production.json'a dokunma)
robocopy publish/ C:\inetpub\Kargoyeri /MIR /XD data-protection-keys logs `
    /XF appsettings.Production.json

# 5. Migration
cd C:\inetpub\Kargoyeri
dotnet ef database update   # otomatik startup'ta da çalışır

# 6. Servisi başlat
Start-Service KargoyeriStudio

# 7. Smoke test
curl https://localhost/health/ready
```

Rollback: önceki versiyonun publish çıktısını yedek tutun (`publish-prev/`); fail olursa `robocopy` tersine çalıştırın.

---

## 7. Acil Durum Kontak Listesi

- **Birincil ops:** ___________ (telefon)
- **Veritabanı yedek lokasyonu:** D:\Backups\Kargoyeri ve s3://egebarkod-backups/
- **Hosting:** ___________ (sunucu IP / panel URL)
- **Domain DNS:** ___________ (Cloudflare/Route53)
- **PTT MüDestek:** 444 1 PTT
- **Aras / MNG / Yurtiçi destek hattı:** Provider portalında

---

## 8. Webhook HMAC İmza (Provider → Studio Inbound)

`POST /api/webhook/{provider}/{tenantKey}` ucuna gelen callback'ler **HMAC-SHA256** ile imzalanır (P1-#1).

### Header formatı
```
X-Kargoyeri-Signature: t=<unix-seconds>,v1=<hex(HMAC_SHA256(secret, "t.body"))>
```

- **Secret:** Tenant Metadata `webhook.inbound.secret` anahtarı (Provider Ayarları → Webhook Sekmesi).
- **Replay penceresi:** `t` mevcut zamana göre ±300 sn (5 dk) içinde olmalı.
- **Birden fazla v1:** Secret rotasyonu için header'da iki `v1=...` parçası bulunabilir; herhangi biri eşleşirse kabul.
- **Body:** Payload byte-byte raw alınır; whitespace değiştirmeyin.

### Test (PowerShell)
```powershell
$secret = "test-secret"
$body   = '{"trackingNumber":"YK123","status":"DELIVERED"}'
$ts     = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$payload = "$ts.$body"
$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [Text.Encoding]::UTF8.GetBytes($secret)
$sig = ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload)) | ForEach-Object { $_.ToString("X2") }) -join ""
Invoke-RestMethod -Method Post -Uri "https://app/api/webhook/Aras/tenantkey" `
  -Headers @{ "X-Kargoyeri-Signature" = "t=$ts,v1=$sig" } `
  -ContentType "application/json" -Body $body
```

### Geriye dönük uyumluluk (geçiş dönemi)
Eski `X-Kargoyeri-Inbound-Secret` header'ı hâlâ kabul edilir; bu yol kullanıldığında loglarda **DEPRECATED** uyarısı yazılır. Provider entegrasyonları HMAC formatına geçtikten sonra bu fallback kaldırılacak (P2 backlog).

### Yaygın hata mesajları
- `timestamp (t) eksik` → header parse edilemedi
- `timestamp pencere disinda (NNNsn > 300sn)` → sunucu saatleri farklı; NTP kontrolü
- `v1 imza eslesmedi` → secret yanlış veya body bozulmuş (proxy/whitespace)

---

## 9. Prometheus `/metrics` (P2-#4)

Kargoyeri Studio, scrape edilebilir bir Prometheus endpoint'i sunar.

```
GET /metrics
```

Cevap formatı: `text/plain; version=0.0.4` (Prometheus exposition format v0.0.4).

### Yayınlanan metrikler

| Metrik | Tip | Etiketler | Açıklama |
|---|---|---|---|
| `kargoyeri_tenants_total` | gauge | – | Sistemdeki tenant sayısı |
| `kargoyeri_shipments_total` | gauge | tenant, provider, status | Tenant + firma + durum bazında gönderi sayısı |
| `kargoyeri_provider_health_success_rate` | gauge | tenant, provider | Son 24 saatlik probe başarı oranı (0–100) |
| `kargoyeri_provider_health_p95_latency_ms` | gauge | tenant, provider | Son 24 saatlik P95 latency |
| `kargoyeri_webhook_retry_pending` | gauge | – | Otomatik retry kuyruğunda bekleyen bildirim |
| `kargoyeri_webhook_retry_abandoned` | gauge | – | 6 deneme sonrası terkedilen webhook |
| `kargoyeri_process_uptime_seconds` | gauge | – | Süreç ayakta kalma süresi |
| `kargoyeri_process_memory_bytes` | gauge | – | GC managed bellek |

### Auth

İsteğe bağlı token koruması — `appsettings.json`:

```json
{ "Studio": { "Metrics": { "Token": "<rastgele-uzun-string>" } } }
```

Token ayarlandığında her scrape isteği `X-Metrics-Token` header'ı veya `?token=` query parametresi içermelidir; aksi halde `401`.

### Prometheus scrape config örneği

```yaml
scrape_configs:
  - job_name: kargoyeri-studio
    scrape_interval: 30s
    metrics_path: /metrics
    static_configs:
      - targets: ['studio.example.com']
    authorization:
      type: Bearer  # veya:
    # http_headers:
    #   X-Metrics-Token: ['<token>']
```

> **Not:** `kargoyeri_shipments_total` üretiminde tenant başına shipment listesi taranır. Çok büyük dağıtımlarda scrape periyodu ≥ 30 sn tutulmalıdır.

---

## 10. KVKK / Veri Sorumlu Notları

- 28 gün üzeri **inactive** tenant verileri arşive alınır (geliştirilmekte — P0-#8).
- Müşteri "verimi sil" talebi → `TenantSwitchController` üzerinden `Deactivate` + 30 gün bekleme + `Hard Delete` (manuel ops).
- Tracking sayfası alıcı bilgisini maskelemiş şekilde gösterir; ham veri sadece tenant kullanıcısına açıktır.

---

## 11. P4-#4 — Multi-Region Failover

### 11.1 Mimarisi

İki veya daha fazla bölgesel instance (örn. `tr-west-1`, `tr-east-1`) aynı veritabanına (read-replica + primary) ya da aktif-aktif veritabanı kümesine bağlanır. Önde bir global load balancer (Cloudflare LB / Azure Front Door / AWS Route 53) `/healthz/regional` probe'unu izler.

```
                   ┌─────────────────┐
   client ───▶     │ Global LB / DNS │
                   │ failover policy │
                   └────────┬────────┘
                ┌───────────┼───────────┐
                ▼           ▼           ▼
         studio-tr-w1  studio-tr-e1  studio-eu-c1
              │             │             │
              └─────────────┴─────────────┘
                       │
              shared / replicated DB
```

### 11.2 `/healthz/regional` Probe

- **Endpoint:** `GET /healthz/regional`
- **Auth:** anonymous (probe için)
- **HTTP 200** → Healthy/Degraded (havuzda kalır)
- **HTTP 503** → Unhealthy (havuzdan düşürülür)
- **JSON payload:** region, instance, version, uptime_seconds, healthy, checks[]

### 11.3 Konfigürasyon

`appsettings.{Region}.json` veya environment variable:

```json
{
  "Studio": {
    "Region": {
      "Code": "tr-west-1",
      "Instance": "studio-1.kargoyeri.local"
    }
  }
}
```

`STUDIO_REGION=tr-west-1` env değişkeni de çalışır (config alınamazsa fallback).

### 11.4 Failover Drill (manuel test)

1. **Adım 1** — Stage'de iki region ayağa kaldır (`studio-a`, `studio-b`).
2. **Adım 2** — `/healthz/regional` her ikisinden de 200 dönmeli.
3. **Adım 3** — `studio-a` üzerinde `Studio:Storage:HealthCheckPath` dizinine yazma izni kaldır → check `Unhealthy` → endpoint **503** dönmeli.
4. **Adım 4** — Global LB 30sn içinde `studio-a`'yı havuzdan düşürmeli (LB konfigine bağlı).
5. **Adım 5** — Trafik tamamen `studio-b`'ye geçer; `Set-Cookie` (auth) kayıplarını önlemek için cookie domain `.kargoyeri.com` olmalı (sticky değil).
6. **Adım 6** — İzni geri ver → 200 döner → otomatik geri ekleme.

### 11.5 RTO / RPO Hedefleri

| Senaryo | RTO | RPO |
|---|---|---|
| Tek instance arızası (aynı bölge) | < 60 sn (LB probe ile) | 0 |
| Tüm bölge arızası | < 5 dk (DNS TTL + LB) | < 60 sn (DB replication lag) |
| Region veritabanı arızası | < 15 dk (manuel failover) | < 5 dk (son backup snapshot'a kadar) |

### 11.6 Multi-Region Tenant Metrics

`tenant-metrics/yyyy-MM-dd.json` dosyaları her instance'ın **kendi** ContentRoot'una yazılır. Çok bölgeli kurulumda bu dosyaları merkezi bir blob depoya (Azure Blob / S3) almak için bir cron şart. Aksi halde billing (P4-#2) yalnızca o instance'ın gördüğü gönderileri sayar.

> **TODO P5:** `TenantMetricsAggregator` arka ucunu pluggable yapmak — local FS yerine S3 backend.
