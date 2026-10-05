# 📄 Dosya Yolu: /docs/USER_GUIDE.md
# 📌 Amac: TurkuazInstaller Community masaustu uygulamasinin temel son kullanici kullanimini aciklar
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Manifest kaynagi, install, update, repair, rollback, progress ve recovery ekran akislarini anlatir
# Bagimli Oldugu Katman: View | Language

# User Guide

## Kaynak

Ana ekranda:

- Paket Kimligi
- Stable veya Beta kanal
- Manifest URL veya local manifest dosya yolu
- Kurulum Dizini

girilir.

Remote manifest HTTPS olmalidir.

Local test ve air-gapped kullanimda local dosya yolu veya file URI kullanilabilir.

## Kur

Kur islemi:

1. release manifestini cozer
2. artifacti indirir veya kopyalar
3. size ve SHA-256 dogrulamasini yapar
4. atomik staging olusturur
5. Velopack paketini uygular
6. kurulu state'i kaydeder

## Guncelle

Guncelle yalniz manifest surumu kurulu surumden yeni ise devam eder.

Apply basarisizsa kurulu state yeni surum olarak kaydedilmez.

## Onar

Onar mevcut kurulu surumle birebir eslesen full package artifactini tekrar uygular.

## Geri Al

Geri Al icin ana manifest mevcut release'i, geri alma manifesti ise onceki release'i gostermelidir.

Rollback basarisizsa mevcut state korunur.

## Iptal ve Tekrar Dene

Calisan operasyon Iptal ile CancellationToken uzerinden sonlandirilabilir.

Failure veya iptal sonrasinda Tekrar Dene son gecerli requesti yeniden calistirir.

## Release Dogrulama

Stable TurkuazInstaller release assetleri Authenticode signed olarak yayinlanir.

ZIP dosyasini elle indirdiyseniz Release sayfasindaki SHA256SUMS.txt ile hash degerini karsilastirabilirsiniz.
