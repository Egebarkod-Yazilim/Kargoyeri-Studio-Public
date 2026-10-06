# Kargoyeri Studio Mimarisi

## Ana Fikir

Tek bir kargo motoru uzerinden birden fazla urun kabugu calistirmak.

## Katmanlar

- Domain
  Kargo, provider, log ve bildirim dili
- Application
  Use-case servisleri ve orkestrasyon
- Infrastructure
  Provider adapterlari, persistence, bildirim dispatch
- Studio Web Host
  Standalone panel
- Embedded Host
  Baska projeye gomulebilir UI kabugu

## Host Stratejisi

- Standalone host ilk gelistirme ortami
- Embedded host ikinci dagitim ortami
- API host opsiyonel entegrasyon kanali

## Fazlar

- Faz 1: standalone urun
- Faz 2: operasyonel guclendirme
- Faz 3: embedded dagitim
