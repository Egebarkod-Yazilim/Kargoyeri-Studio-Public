# Observability (P5-#5)

Kargoyeri Studio uc katmanli gozlemleme kullanir:

| Katman    | Arac                       | Amac                                      |
|-----------|----------------------------|-------------------------------------------|
| Loglar    | Serilog -> Seq + dosya     | Yapilandirilmis log, correlation ID       |
| Hatalar   | Sentry                     | Exception + breadcrumb + release tracking |
| Tracing   | OpenTelemetry -> OTLP      | Distributed trace, metrics                |

## 1. Loglar (Serilog)

Konfig: `Serilog:Seq:ServerUrl` (+ opsiyonel `ApiKey`).
Seq URL bos ise sadece Console + `logs/studio-YYYYMMDD.log` (rolling, 14 gun).

Her satirda otomatik enrichment:
- `CorrelationId` (CorrelationIdMiddleware tarafindan)
- `MachineName`, `ProcessId`, `ThreadId`
- `Application=Kargoyeri.Studio`, `Environment=<env>`

## 2. Sentry

Konfig: `Studio:Sentry:Dsn` (ENV: `Studio__Sentry__Dsn`).
Dsn bos ise Sentry tamamen pasiftir — uretim build'i etkilemez.

Aktif oldugunda:
- `MinimumEventLevel = Error` -> sadece error+ Sentry'e gider
- `TracesSampleRate` Production'da 0.1, Dev'de 1.0
- `SendDefaultPii = false` -> KVKK uyumlu, kullanici bilgisi sizmaz
- Release: assembly version
- Environment: `ASPNETCORE_ENVIRONMENT`

### Sentry kurulum
1. https://sentry.io -> Project: Kargoyeri.Studio (ASP.NET Core)
2. DSN'i kopyalayip ENV olarak set et:
   ```bash
   export Studio__Sentry__Dsn="https://abc@o123.ingest.sentry.io/456"
   ```
3. Restart. Loglarda `Sentry enabled. Environment=Production` gorulur.

## 3. OpenTelemetry

Konfig:
- `Studio:Otel:Endpoint` (OTLP gRPC, ornek `http://otel-collector:4317`)
- `Studio:Otel:ServiceName` (default `kargoyeri-studio`)
- `Studio:Region` (resource attribute, default `tr-central`)

Endpoint bos ise OTel pasif.

### Topladigi sinyaller
**Tracing (`WithTracing`):**
- ASP.NET Core HTTP server
- HttpClient outbound
- SqlClient (statement metni gonderilmez — PII guard)
- EntityFrameworkCore

**Metrics (`WithMetrics`):**
- ASP.NET Core (request duration, throughput)
- HttpClient
- Runtime (GC, thread pool, exceptions)

### OTel Collector ornek konfig (`otel-collector.yaml`)
```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317

exporters:
  prometheus:
    endpoint: 0.0.0.0:8889
  loki:
    endpoint: http://loki:3100/loki/api/v1/push
  jaeger:
    endpoint: jaeger:14250
    tls:
      insecure: true

service:
  pipelines:
    traces:
      receivers: [otlp]
      exporters: [jaeger]
    metrics:
      receivers: [otlp]
      exporters: [prometheus]
```

### Docker Compose snippet
```yaml
otel-collector:
  image: otel/opentelemetry-collector-contrib:latest
  ports: ["4317:4317", "4318:4318", "8889:8889"]
  volumes:
    - ./otel-collector.yaml:/etc/otelcol-contrib/config.yaml
```

## 4. Health checks

`GET /healthz` -> ASP.NET Core HealthChecks
`GET /healthz/regional` -> JSON: region/instance/version/checks (P4 multi-region)

## 5. Production checklist

- [ ] `Studio__Sentry__Dsn` ENV set
- [ ] `Studio__Otel__Endpoint` ENV set (collector calisir durumda)
- [ ] `Serilog__Seq__ServerUrl` ENV set
- [ ] Release version assembly'de dogru — `<Version>` csproj'da bump edilsin
- [ ] Sentry alert kurali: error volume > 50/min -> Slack/PagerDuty
- [ ] Grafana dashboard: `kargoyeri-studio` service, p95 latency, error rate
