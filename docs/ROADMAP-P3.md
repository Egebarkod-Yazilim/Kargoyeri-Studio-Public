# P3 Backlog — Customer-facing UX & Polish

P0 + P1 + P2 üretim olgunluğu bittikten sonraki **görünür** iyileştirmeler.
Hedef: müşterinin ve müşteri-müşterisinin (alıcı) gözünde **modern, akıcı, takip edilebilir** bir deneyim.

| # | Konu | Hedef | Durum |
|---|---|---|---|
| **P3-#1** | Public tracking — visual progress stepper | `/track/{tn}` sayfasında yatay 5-adımlı durum çubuğu (Hazır / Kargoda / Yolda / Dağıtımda / Teslim) ikonlu | ✅ done |
| **P3-#2** | Tenant dashboard — KPI widget'ları | Bugün/haftalık özet, durum dağılımı (mini bar chart), kargo firma payı (donut), son hareketler feed | ✅ done |
| **P3-#3** | Toast notification system | Üst sağda otomatik kaybolan bildirim (success/info/warn/error), TempData replacement | ✅ done |
| **P3-#4** | Empty-state illustrations | Liste boşken ikon + "Henüz veri yok, ilk ... ekleyin" prompt'u (Shipments, Pickup, Notifications) | ✅ done |
| **P3-#5** | Tenant onboarding wizard | Yeni tenant'a 4 adımlı kurulum (firma seç → API key gir → test gönderi → webhook URL) | ✅ done |
| **P3-#6** | Command palette (Ctrl+K) | Hızlı arama: gönderi/müşteri/sayfa, klavye navigasyonu | ✅ done |
| **P3-#7** | Dark mode toggle | CSS değişkenleri + cookie tercih, tüm sayfalarda destek | ✅ done |
| **P3-#8** | Mobile responsive sweep | Sidebar collapse, tablet/phone breakpoints, touch hedef boyutu | ✅ done |

> P4 (gelecek): tenant.metrics aggregator, multi-region failover, billing module, customer-facing dashboard widget'ları (iframe-embed).
