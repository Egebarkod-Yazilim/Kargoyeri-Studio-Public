# Kargoyeri Studio

Kargoyeri Studio, mevcut `Kargoyeri` cekirdeginin ustune kurulan yeni urun hostudur.

## Neyi Cozuyor?

- Standalone yonetim paneli saglar
- Pazaryeri gibi sistemlere gomulebilir host mantigini hazirlar
- Ayni cekirdekle birden fazla dagitim modeli kurulmasina izin verir

## Cozum Yapisi

- `src/Kargoyeri.Studio.Web`
  Bagimsiz calisan yonetim paneli
- `src/Kargoyeri.Studio.Embedded`
  Host projelere gomulmek icin Razor Class Library kabugu
- `../Kargoyeri/src/*`
  Yeniden kullanilan core katmanlar

## Baslangic

```powershell
dotnet build .\Kargoyeri.Studio\Kargoyeri.Studio.sln --configfile .\Kargoyeri.Studio\NuGet.Config
dotnet run --project .\Kargoyeri.Studio\src\Kargoyeri.Studio.Web\Kargoyeri.Studio.Web.csproj --configfile .\Kargoyeri.Studio\NuGet.Config
```

## Ekranlar

- Dashboard
- Workspace
- Kargo Firmalari
- Gonderiler
- Bildirimler
- Roadmap
