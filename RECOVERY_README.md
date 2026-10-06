# Kargoyeri Studio Recovery

Bu klasor, projenin kurtarilmis ve derleme dogrulamasi yapilmis son calisir kopyasidir.

## Dogru proje yolu

`C:\Users\Yazilim\Desktop\Kargoyeri.Studio`

## Acilacak cozum dosyasi

Tercih edilen:

`C:\Users\Yazilim\Desktop\Kargoyeri.Studio\Kargoyeri.Studio.Clean.sln`

Alternatif:

`C:\Users\Yazilim\Desktop\Kargoyeri.Studio\Kargoyeri.Studio.sln`

## Dogrulama

Bu klasorde asagidaki komut basarili calisti:

`dotnet build .\Kargoyeri.Studio.sln`

Sonuc:

- `0 Warning`
- `0 Error`

## Notlar

- Cozum artik yalnizca 3 proje icerir:
  - `Kargoyeri.Studio.Core`
  - `Kargoyeri.Studio.Web`
  - `Kargoyeri.Studio.Embedded`
- `Kargoyeri.Application`, `Kargoyeri.Contracts`, `Kargoyeri.Domain`, `Kargoyeri.Infrastructure`
  derleme bagimliliklari `src\Kargoyeri.Studio.Core\lib` altinda yerel DLL olarak sabitlenmistir.
- Visual Studio daha once eski klasoru veya eski `.vs` cache'ini acmissa yanlis hata gosterebilir.
  Bu durumda sadece bu klasordeki `Kargoyeri.Studio.Clean.sln` dosyasini acin.
