# Database Migrations — Disiplin Rehberi

## Mevcut durum

- **EF Core 8.0.8** + SQL Server backend
- DbContext: `Kargoyeri.Infrastructure.Persistence.SqlServer.KargoyeriDbContext`
- Migration klasoru: `Projeler/KargoEntegre--Full-Otomatik-master/Kargoyeri/src/Kargoyeri.Infrastructure/Persistence/SqlServer/Migrations/`
- **Otomatik uygulama:** `Program.cs` baslangicta `db.Database.MigrateAsync()` cagiriyor — pending migration'lar uygulamanin acilisinda otomatik yurutuluyor.
- History tablosu: `__EFMigrationsHistory` (EF Core varsayilan)

## Helper script

Tum komutlar repo kokunden calistirilmali:

```powershell
# Mevcut migrations
.\scripts\migrate.ps1 -Action list

# Yeni migration
.\scripts\migrate.ps1 -Action add -Name AddBillingHistoryTable

# DB'yi guncel migration'a getir
.\scripts\migrate.ps1 -Action update

# Belirli migration'a kadar geri donus
.\scripts\migrate.ps1 -Action update -Target 20260424123910_InitialCreate

# Iki migration arasi idempotent SQL uret (production deployment icin)
.\scripts\migrate.ps1 -Action script -From InitialCreate -To AddBillingHistoryTable

# Son migration'i geri al (henuz DB'ye uygulanmadiysa)
.\scripts\migrate.ps1 -Action remove

# DEV ONLY: DB'yi sifirla
.\scripts\migrate.ps1 -Action drop
```

## Workflow — yeni migration eklemek

1. Domain modeli degistir (`Kargoyeri.Domain` veya `Kargoyeri.Infrastructure` icinde entity/configuration)
2. `.\scripts\migrate.ps1 -Action add -Name <SemanticName>`
3. Olusan migration `.cs` dosyasini **incele** — EF bazen istemedigin DROP/CREATE uretebilir
4. Local DB'de dene: `.\scripts\migrate.ps1 -Action update`
5. Test ettikten sonra commit'le

## Workflow — production deployment

**Otomatik mod (varsayilan):** Yeni surum deploy edilince `MigrateAsync()` pending migration'lari uygular. Downtime gerektirmez (single-region) ama:

- **Buyuk tablolarda yavas migration'a dikkat:** `ALTER TABLE` lock alabilir
- **Breaking schema degisiklikleri:** once additive migration deploy et (yeni kolon nullable), kod degisikligi sonraki release, eski kolon silimi 3. release

**Manuel mod (yuksek-riskli migration):**

```powershell
# 1. Idempotent SQL uret
.\scripts\migrate.ps1 -Action script -From <CurrentInDb> -To <NewMigration>

# 2. Olusan SQL'i DBA review'a gonder
# 3. Bakim penceresinde uygula:
sqlcmd -S <server> -d KargoyeriStudio -i .\scripts\migration-<...>.sql

# 4. Uygulama deploy
# 5. Dogrula: SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId DESC
```

> Otomatik mod'u kapatmak icin `Program.cs`'de `MigrateAsync` cagrisini env-flag ile kosullu yap (`Studio:Db:AutoMigrate=false`).

## Geri donus (rollback)

EF migrations geri donus = onceki migration'a `update` cekmek:

```powershell
.\scripts\migrate.ps1 -Action update -Target 20260424123910_InitialCreate
```

**Veri kaybi riski:** Down migration `DROP COLUMN`/`DROP TABLE` icerebilir. Production'da rollback yerine **forward-fix** stratejisi tercih edilir (yeni migration ile geri al).

## kargoyeri_migration.sql — DEPRECATED

`kargoyeri_migration.sql` dosyasi EF Migration sistemi devreye girmeden once **manuel kurulum** icin kullaniliyordu. **Yeni kurulumlarda kullanilmamalidir.** Yeni instance'larda otomatik `MigrateAsync` calisir; bos DB'ye `InitialCreate` migration'ini kendi olusturur.

Eski deployment'lar icin tutuluyor; bir sonraki major release'de silinecek.

## Yeni envanter eklemeleri

Yeni `IEntityTypeConfiguration<T>` veya `DbSet<T>` ekledikten sonra **mutlaka migration olustur**. Aksi halde startup'ta `db.Database.MigrateAsync()` farkli sema farki bulamaz, `EnsureCreated` yerine migration history kontrol eder ve yeni tablo **olusmaz**.

## Connection string

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Studio": "Server=localhost;Database=KargoyeriStudio;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

`Studio:Db:Mode = "SqlServer"` set edilmelidir; aksi halde JSON-based fallback repository kullanilir ve migration calismaz.

## Yararli komutlar

```powershell
# DbContext modeline gore beklenen DDL'i gor (DB'ye yazmadan)
dotnet ef dbcontext script --project ... --startup-project ...

# Pending migration var mi?
dotnet ef migrations has-pending-model-changes --project ...

# Tum migration'larin SQL output'u (idempotent — istedigin yerde calistirilabilir)
dotnet ef migrations script --idempotent --output full-history.sql --project ...
```
