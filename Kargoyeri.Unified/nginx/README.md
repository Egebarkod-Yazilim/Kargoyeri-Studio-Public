# Nginx Reverse Proxy — Kargoyeri.Studio

## Yapilandirma dosyalari
- `nginx.conf` — global ayarlar (worker, gzip, rate limit zone, upstream).
- `conf.d/studio.conf` — site (HTTP→HTTPS yonlendirme + HTTPS reverse proxy).
- `certs/` — SSL sertifikalari (`fullchain.pem`, `privkey.pem`).

## SSL sertifikasi koyma

### 1) Let's Encrypt (production)
```bash
sudo certbot certonly --standalone -d studio.firma.com
sudo cp /etc/letsencrypt/live/studio.firma.com/fullchain.pem ./nginx/certs/
sudo cp /etc/letsencrypt/live/studio.firma.com/privkey.pem   ./nginx/certs/
```

### 2) Self-signed (test / intranet)
```bash
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout nginx/certs/privkey.pem -out nginx/certs/fullchain.pem \
  -subj "/CN=studio.local"
```

### 3) Kurumsal CA
`fullchain.pem` icine sertifika + ara sertifika zincirini, `privkey.pem` icine ozel anahtari koyun.

## Calistirma
```bash
# Repo'nun bir ust dizininde (Desktop)
docker compose -f Kargoyeri.Studio/docker-compose.yml up -d --build
```

## Health endpoint'leri
- `GET /health`        — detayli (storage, db, vs.)
- `GET /health/live`   — process ayakta mi?
- `GET /health/ready`  — bagimliliklarla birlikte hazir mi?

Disariya kapatmak icin `studio.conf` icindeki `allow/deny` direktiflerini acin.

## Rate limit
- `Access/Login` — 10 req/dk + burst 5
- `/api/*`       — 60 req/dk + burst 30
- Uygulama icinde de ek rate limit var (`AddRateLimiter`).

## Forwarded headers
Studio (Program.cs) `UseForwardedHeaders` ile `X-Forwarded-For` ve `X-Forwarded-Proto` baslarini okur. Gercek client IP rate limiter'da ve loglarda dogru gorunur.
