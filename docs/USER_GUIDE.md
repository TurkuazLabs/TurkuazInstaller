# 📄 Dosya Yolu: /docs/USER_GUIDE.md
# 📌 Amac: TurkuazInstaller Community masaustu uygulamasinin temel son kullanici kullanimini aciklar
# 📌 Modul - Markdown
# Version: 1.0.1
# Aciklama: Manifest-driven target, install, update, repair, rollback, uninstall, progress ve recovery ekran akislarini anlatir
# Bagimli Oldugu Katman: View | Language

# User Guide

## Kaynak

Ana ekranda:

- Paket Kimligi
- Stable veya Beta kanal
- Manifest URL veya local manifest dosya yolu
- optional Kurulum Dizini

bulunur.

Kurulum Dizini bos birakilirsa manifest install.target degeri kullanilir.

Remote manifest HTTPS olmalidir.

Local test ve air-gapped kullanimda local dosya yolu veya file URI kullanilabilir.

## Manifest Policy

Stable v1 runtime manifestten gercek olarak su alanlari uygular:

- full install mode
- SHA-256
- optional Authenticode
- windows-build prerequisite
- architecture prerequisite
- dotnet-desktop-runtime prerequisite
- relative preserve_paths
- rollback supported policy

Desteklenmeyen prerequisite sessizce atlanmaz; islem durdurulur.

## Kur

Kur islemi:

1. release manifestini cozer
2. prerequisites kontrol edilir
3. artifact indirilir veya kopyalanir
4. size ve SHA-256 dogrulanir
5. manifest Authenticode istiyorsa Windows trust verification yapilir
6. atomik staging olusturulur
7. Velopack paketini uygular
8. kurulu state kaydedilir

## Guncelle

Guncelle yalniz manifest surumu kurulu surumden yeni ise devam eder.

Manifest prerequisites ve preserve_paths politikasi update planina tasinir.

Apply basarisizsa kurulu state yeni surum olarak kaydedilmez.

## Onar

Onar mevcut kurulu surumle birebir eslesen full package artifactini tekrar uygular.

## Geri Al

Geri Al yalniz current manifest rollback.supported=true ise calisir.

Ana manifest mevcut release'i, geri alma manifesti ise onceki release'i gostermelidir.

Rollback basarisizsa mevcut state korunur.

## Kaldir

Kaldir islemi manifest gerektirmez.

Paket Kimligi ile kaydedilmis install state bulunur ve kurulum rootundaki Velopack Update.exe uninstall komutu calistirilir.

Velopack uninstall basarili olduktan sonra TurkuazInstaller state kaydi silinir.

## Iptal ve Tekrar Dene

Calisan operasyon Iptal ile CancellationToken uzerinden sonlandirilabilir.

Failure veya iptal sonrasinda Tekrar Dene son gecerli requesti yeniden calistirir.

## Release Dogrulama

Stable TurkuazInstaller release assetleri production signing gate tamamlandiktan sonra Authenticode signed olarak yayinlanir.

ZIP dosyasi SHA256SUMS.txt ve GitHub artifact attestation ile dogrulanabilir.
