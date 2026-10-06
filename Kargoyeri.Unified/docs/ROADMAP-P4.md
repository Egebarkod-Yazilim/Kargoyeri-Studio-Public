# P4 Backlog — Scale, Money & Embeddable

P0 + P1 + P2 + P3 tamamlandi. P4 hedefleri: **olcek**, **gelir**, **dis-entegrasyon**.

| # | Konu | Hedef | Durum |
|---|---|---|---|
| **P4-#1** | Tenant.metrics aggregator | Gunluk rollup snapshot (per-tenant: shipment count, by-status, by-provider) JSON disk + admin endpoint | ✅ done |
| **P4-#2** | Billing module | Aylik kullanim hesabi (gonderi sayisi x tier fiyati), HTML fatura + admin liste | ✅ done |
| **P4-#3** | Customer-facing iframe widget | Public read-only mini-tracker `/embed/track/{tn}`, iframe-safe headers | ✅ done |
| **P4-#4** | Multi-region failover | `/healthz/regional` probe + RUNBOOK §4 failover prosedurleri | ✅ done |

> P5 (gelecek): self-service signup + kredi karti odeme (iyzico/Stripe), full event-sourcing, AI-powered ETA tahmini, mobil push (FCM/APNs).
