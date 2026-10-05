# 📄 Dosya Yolu: /docs/PROJECT_INTEGRATION.md
# 📌 Amac: TurkuazInstaller'i NSIS yerine kullanacak projeler icin ortak entegrasyon ve migration standardini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Velopack full package, manifest, prerequisite, preserve path, rollback, uninstall ve kabul testlerini tum projeler icin standartlastirir
# Bagimli Oldugu Katman: Service | Tool | Config

# Project Integration Standard

Bu dokuman TurkuazLabs, TurkuazSoft ve LevelUpGT masaustu projelerinin NSIS veya proje-ozel installer yapilarindan TurkuazInstaller'a gecis standardidir.

## Hedef Model

Her uygulama kendi release artifactini uretir.

TurkuazInstaller:

1. release manifestini bulur
2. prerequisite kontrolu yapar
3. artifact size ve SHA-256 dogrular
4. manifest Authenticode istiyorsa signature verification yapar
5. Velopack paketini uygular
6. install state kaydeder
7. update, repair, rollback ve uninstall islemlerini ayni package engine uzerinden yonetir

## Stable v1 Paket Standardi

Stable v1 icin:

- architecture: win-x64
- install mode: full
- initial install artifact: Velopack Setup.exe
- update/repair/rollback artifact: full nupkg
- delta package zorunlu degildir
- remote manifest/artifact: HTTPS
- local test: file path veya file URI
- SHA-256: zorunlu
- Authenticode: urun policy'sine gore optional
- production TurkuazInstaller binary release: signed

## Manifest

Baslangic sablonu:

examples/project-manifest.template.yml

Her release icin en az:

- package.id
- package.version
- package.channel
- artifact.uri
- artifact.sha256
- artifact.size_bytes
- install.mode
- install.target
- rollback.supported

alanlari gercek degerlerle uretilmelidir.

## Package ID

Package ID kalici olmalidir.

Uygulama adi degisse bile ayni kurulu urunu temsil eden package id degistirilmemelidir.

Ornek:

```text
turkuazlabs.unizip
turkuazlabs.jexporter
turkuazlabs.turkuazoffice
turkuazsoft.nova9
levelupgt.ragnarokpacketviewer
```

## Install Target

Varsayilan kullanici-bazli hedef:

```text
%LOCALAPPDATA%/Programs/<ProductName>
```

Program Files gerektiren urunlar ayri elevation policy ile degerlendirilmelidir.

## Prerequisites

Stable v1 built-in prerequisite id degerleri:

- windows-build
- architecture
- dotnet-desktop-runtime

Desteklenmeyen prerequisite sessizce atlanmaz.

## Preserve Paths

Kullanici verisi uygulama binary klasorunden ayrilmalidir.

preserve_paths:

- install rootuna gore relative olmalidir
- install root disina cikamaz
- Velopack current altinda olamaz

Tercih edilen model:

```text
<install-root>/
  current/
  packages/
  UserData/
```

## Rollback

Rollback kullanan projede:

- current release manifesti rollback.supported=true olmali
- onceki full nupkg erisilebilir olmali
- package id ayni olmali
- onceki version current versiondan eski olmali

## NSIS Migration Sirasi

Bir projede NSIS kaldirilmadan once:

1. mevcut installer davranislari envanteri cikarilir
2. install target belirlenir
3. kullanici/config/data klasorleri preserve policy'ye ayrilir
4. Velopack full package uretilir
5. installer manifesti eklenir
6. TurkuazInstaller ile temiz install testi yapilir
7. onceki -> yeni version update testi yapilir
8. ayni version repair testi yapilir
9. yeni -> onceki rollback testi yapilir
10. uninstall testi yapilir
11. mevcut kullanici verisinin korundugu dogrulanir
12. CI yesil olduktan sonra NSIS release adimi kaldirilir

NSIS scripti ilk migration PR'inda silinmemelidir.

Once TurkuazInstaller pipeline'i yesil calistirilir; eski installer ancak yeni yol dogrulandiktan sonra kaldirilir.

## Kabul Kriterleri

Bir proje TurkuazInstaller'a gecmis kabul edilmek icin:

- clean install basarili
- update basarili
- repair basarili
- rollback basarili veya urun policy'sinde acikca disabled
- uninstall basarili
- user data korunuyor
- manifest SHA-256 gercek artifact ile eslesiyor
- unsupported prerequisite fail-closed
- CI release artifacti uretiyor

## Sonraki Proje

Her repo migration'i ayri PR olarak yapilmalidir.

TurkuazInstaller Core'a proje-ozel logic eklenmemelidir.

Proje-ozel davranis manifest, release pipeline veya ilgili uygulama reposunda kalmalidir.
