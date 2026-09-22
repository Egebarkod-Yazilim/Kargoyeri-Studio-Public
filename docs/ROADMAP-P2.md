# P2 Backlog — Production Hardening

P0 (ilk müşteri öncesi) ve P1 (ilk 1-2 ay) bittikten sonra üretim olgunluğu için sıradaki işler.

| # | Konu | Hedef | Durum |
|---|---|---|---|
| **P2-#1** | Per-tenant API rate limit | X-Api-Key bazlı, tenant'a özgü kotalar (ör. 1000 req/dakika) | ✅ done |
| **P2-#2** | Audit log CSV export | `/audit` ekranından tarih aralığı seçip CSV indirme | ✅ done |
| **P2-#3** | Outbound webhook retry & dashboard | Müşteriye gönderilen bildirim webhook'larının kuyruk/retry/durum görünümü | ✅ done |
| **P2-#4** | Prometheus `/metrics` endpoint | scrape edilebilir counter/histogram (shipments, errors, latency) | ✅ done |
| **P2-#5** | Tenant impersonation audit | Admin "Switch" yaptığında özel audit kaydı + banner | ✅ done |
| **P2-#6** | Soft-delete shipments | `IsArchived` flag + listelerde filtre | ✅ done |
| **P2-#7** | Multi-language (TR/EN) | `IStringLocalizer` + culture cookie | ✅ done |
| **P2-#8** | Backup verification scripti | Haftalık `restore-verify.ps1` — yedek dosyasının açılabilirliği | ✅ done |

> P3 (ileri seviye): tenant.metrics aggregator, multi-region failover, billing module, customer-facing dashboard widget'ları.
