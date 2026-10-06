# Veri Saklama ve Silme Politikasi (Data Retention)

Bu doküman Kargoyeri Studio'da hangi verinin ne kadar süre saklandığını ve nasıl silindiğini tanımlar.
KVKK 4. madde "ölçülülük" ve 7. madde "silme/yok etme" ilkelerine uygundur.

---

## 1. Veri Sınıfları ve Saklama Süreleri

| Veri Sınıfı | Örnek Alanlar | Saklama Süresi | Gerekçe |
|---|---|---|---|
| **Aktif gönderi** | Alıcı adı/telefonu/adresi, paket bilgisi, tracking no | 24 ay | KVKK ölçülülük + müşteri hizmetleri ihtiyacı |
| **Operasyon log** | Kim hangi işlemi yaptı, IP, payload özeti | 12 ay | Audit/güvenlik (denetim için minimum) |
| **Bildirim log** | SMS/e-posta gönderim kayıtları | 12 ay | Şikâyet çözümü |
| **Faturalama / muhasebe** | Fatura no, tutar, vergi kayıtları | 10 yıl | Vergi Usul Kanunu md. 253 |
| **Kullanıcı hesabı** | E-posta, hash'li şifre, ad | Hesap aktif olduğu sürece + 30 gün | Yeniden açma için bekleme |
| **Tenant metadata** | İşletme adı, ayarlar, provider credential'ları | Tenant aktif olduğu sürece | İşlevsel zorunluluk |
| **Audit raporları** | Aylık özetler | 24 ay | İç denetim |
| **Web log dosyaları** | Erişim log'u (logs/studio-*.log) | 14 gün | Disk yönetimi + güvenlik analizi |

---

## 2. Otomatik Temizleme Job'ları

> **Not (P1-#3 tamamlandı):** Otomatik temizleme `DataRetentionService` BackgroundService'i tarafından **24 saatte bir** çalıştırılır. Manuel SQL'ler yedek/acil kullanım için aşağıda saklanır.
>
> İş davranışı:
> - 24 aydan eski gönderiler `Recipient.*` alanları nullify + `kvkk.anonymized=true` flag'i set
> - KVKK silme talebi onaylanmış (`status=approved`) ve `HardDeleteAt <= now` olan tenant'ların **tüm** gönderileri anonimleştirilir; Metadata `status=executed` olur
> - Idempotent: zaten `kvkk.anonymized=true` olan kayıtlar atlanır

### 2.1 Eski log dosyaları
Serilog rolling file zaten **14 gün retention** uyguluyor (Program.cs).

### 2.2 24 aydan eski gönderi verisi (anonimleştirme)
```sql
-- Manuel çalıştırılır, ayda bir
UPDATE CargoShipments
SET RecipientName    = 'ANONIMLESTIRILMIS',
    RecipientPhone   = NULL,
    RecipientAddress = '[ANONIM]',
    SenderPhone      = NULL,
    Notes            = NULL
WHERE CreatedAtUtc < DATEADD(MONTH, -24, GETUTCDATE())
  AND IsAnonymized   = 0;

UPDATE CargoShipments SET IsAnonymized = 1
WHERE CreatedAtUtc < DATEADD(MONTH, -24, GETUTCDATE());
```
> Faturalama referansı (OrderReference, CollectionAmount) korunur — VUK 10 yıl.

### 2.3 12 aydan eski operasyon logları
```sql
DELETE FROM ShipmentOperationLogs
WHERE OccurredAtUtc < DATEADD(MONTH, -12, GETUTCDATE());
```

### 2.4 Inactive tenant
30 günden fazla giriş yapmamış tenant → "DataRetentionWarning" e-postası gönderilir.
60 gün sonra "soft-delete" (Disabled = true).
180 gün sonra hard-delete (faturalama kayıtları hariç).

---

## 3. Müşteri "Verilerimi Sil" Talebi (KVKK md. 7)

### Akış
1. Kullanıcı `/legal/data-deletion` sayfasından veya `kvkk@kargoyeri.com` üzerinden talep eder.
2. **Kimlik doğrulama** (T.C. kimlik no + e-posta + son login IP eşleşmesi).
3. Admin panel → "KVKK Talepleri" sekmesi → talep onaylanır (P1).
4. Hesap **soft-delete** (30 gün geri alınabilir).
5. 30 gün sonra **hard-delete**:
   - Kullanıcı kaydı silinir.
   - İlişkili gönderiler anonimleştirilir (tutar/tarih korunur — VUK).
   - Operasyon log'larında kullanıcı kimliği `[SILINMIS-USER]` olarak değiştirilir.
6. Talep sahibine yazılı bildirim (e-posta).

### Süreler (KVKK)
- Talep yanıt süresi: **30 gün** (KVKK md. 13).
- Ücretsiz (basit talepler).

---

## 4. Tracking Sayfası — Public Veri Maskeleme

`/track/{trackingNumber}` sayfasında alıcı bilgisi otomatik maskelenir:
- Alıcı adı: `Ahmet Y****` (ilk isim + soyad ilk harf + 4 yıldız)
- Adres: gizli — sadece **il** gösterilir (district bile yok)
- Telefon: hiç gösterilmez
- Tracking timeline: sadece statü change event'leri; provider error payload'ı sızdırılmaz

Bu davranış `Controllers/TrackingController.cs` `MaskName` ve `BuildTimeline` metodlarındadır.

---

## 5. Yedeklerin KVKK Durumu

Yedekler de kişisel veri içerir → KVKK kapsamındadır:
- Yedek dosyaları **şifreli disk** üzerinde tutulur.
- 14 günden eski .bak dosyaları otomatik silinir (`scripts/backup-sqlserver.ps1`).
- Off-site yedek (S3/Azure Blob) → server-side encryption + access policy ile izole.
- Anonimleştirme yapılmış kayıtlar yedeklerde de **anonim** olarak görünür (yedek geri yüklenince anonimleştirme tekrar uygulanmalı — bu sebeple anonimleştirme job'u ayda bir tekrar çalıştırılır).

---

## 6. Sorumluluklar

| Rol | Sorumluluk |
|---|---|
| **Sistem yöneticisi** | Aylık anonimleştirme SQL'lerini çalıştırma; yedek doğrulama |
| **KVKK iletişim noktası** | kvkk@kargoyeri.com — talepleri 30 gün içinde yanıtlama |
| **Geliştirici ekip** | Yeni bir veri alanı eklerken bu dokümana satır ekleme |

---

## 7. Politika Değişiklik Log'u

| Tarih | Değişiklik | Yapan |
|---|---|---|
| 2026-04-28 | İlk sürüm — P0-#8 | Studio team |
