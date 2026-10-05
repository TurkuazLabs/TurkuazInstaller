# 📄 Dosya Yolu: /docs/PROJECT_INTEGRATION.md
# 📌 Amac: TurkuazInstaller'i NSIS yerine kullanacak projeler icin ortak entegrasyon ve migration standardini tanimlamak
# 📌 Modul - Markdown
# Version: 1.1.2
# Aciklama: Signed manifest trust, Velopack full package, prerequisite, preserve path, rollback, uninstall ve kabul testlerini tum projeler icin standartlastirir
# Bagimli Oldugu Katman: Service | Repo | Tool | Config

# Project Integration Standard

Bu dokuman TurkuazLabs, TurkuazSoft ve LevelUpGT masaustu projelerinin NSIS veya proje-ozel installer yapilarindan TurkuazInstaller'a gecis standardidir.

## Hedef Model

Her uygulama kendi release artifactini, installer manifestini ve detached manifest signature dosyasini uretir.

TurkuazInstaller:

1. release manifestini ve `.p7s` sidecarini bulur
2. package id icin external manifest trust policy resolve eder
3. manifest CMS signature + publisher + certificate SHA-256 pinini dogrular
4. manifesti parse eder
5. prerequisite kontrolu yapar
6. artifact size ve SHA-256 dogrular
7. manifest Authenticode istiyorsa artifact signature verification yapar
8. Velopack paketini uygular
9. install state kaydeder
10. update, repair, rollback ve uninstall islemlerini ayni package engine uzerinden yonetir

## Stable v1 Paket Standardi

Stable v1 icin:

- architecture: win-x64
- install mode: full
- initial install artifact: Velopack Setup.exe
- update/repair/rollback artifact: full nupkg
- delta package zorunlu degildir
- remote manifest/artifact: HTTPS
- local test: file path veya file URI
- manifest CMS detached signature: zorunlu
- manifest certificate SHA-256 pin: zorunlu external trust config
- artifact SHA-256: zorunlu
- artifact Authenticode: urun policy'sine gore optional
- production TurkuazInstaller binary release: signed

## Manifest

Baslangic sablonu:

examples/project-manifest.template.yml

Manifest signer trust sablonu:

examples/manifest-trust.template.yml

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

Manifest publish edilirken ayni raw byte'lar CMS/PKCS#7 detached olarak imzalanir.

Ornek dosya ciftleri:

```text
installer-manifest.yml
installer-manifest.yml.p7s
```

Trust store release kaynagindan indirilmez.

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

## Manifest Trust Provisioning

Package release pipeline kullanilmadan once:

1. manifest signer certificate belirlenir
2. certificate subject kaydedilir
3. certificate SHA-256 hash kaydedilir
4. package id icin `manifest-trust.yml` entry provision edilir
5. bu trust config manifest hosting kaynagindan bagimsiz dagitilir

Default desktop trust store yolu:

```text
%LOCALAPPDATA%/TurkuazInstaller/config/manifest-trust.yml
```

Signer certificate rotate edilecekse trust store once guncellenir, yeni signer ile manifest daha sonra publish edilir.

## Install Target

Varsayilan kullanici-bazli hedef:

```text
%LOCALAPPDATA%/Programs/<ProductName>
```

Program Files gerektiren urunlar ayri elevation policy ile degerlendirilmelidir.

## Prerequisites

Community v1.1 built-in prerequisite id degerleri:

- windows-build
- architecture
- dotnet-desktop-runtime

Desteklenmeyen prerequisite sessizce atlanmaz.

Eksik prerequisite otomatik kurulacaksa manifestte explicit install policy tanimlanir.

Prerequisite installer icin:

- HTTPS veya file URI
- direct .exe artifact
- gercek SHA-256 ve size
- Authenticode signature
- exact publisher subject
- optional certificate SHA-256 pin
- shell-free argument listesi
- explicit elevation policy

zorunlu guvenlik siniridir.

Installer calistiktan sonra prerequisite tekrar detect edilir. Re-probe basarisizsa ana paket kurulmaz.

Reboot gerektiren installer sonucu reboot/resume P1 tamamlanana kadar fail-closed durur.

Detay:

docs/PREREQUISITES.md

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
- onceki manifest + `.p7s` erisilebilir olmali
- package id ayni olmali
- onceki version current versiondan eski olmali

## NSIS Migration Sirasi

Bir projede NSIS kaldirilmadan once:

1. mevcut installer davranislari envanteri cikarilir
2. install target belirlenir
3. kullanici/config/data klasorleri preserve policy'ye ayrilir
4. Velopack full package uretilir
5. installer manifesti eklenir
6. manifest signer trust entry provision edilir
7. manifest `.p7s` detached signature release pipeline'a eklenir
8. TurkuazInstaller ile temiz install testi yapilir
9. onceki -> yeni version update testi yapilir
10. ayni version repair testi yapilir
11. yeni -> onceki rollback testi yapilir
12. uninstall testi yapilir
13. mevcut kullanici verisinin korundugu dogrulanir
14. CI yesil olduktan sonra NSIS release adimi kaldirilir

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
- manifest `.p7s` signature mevcut ve valid
- manifest publisher subject external trust policy ile eslesiyor
- manifest certificate SHA-256 pin external trust policy ile eslesiyor
- artifact SHA-256 gercek artifact ile eslesiyor
- unsupported prerequisite fail-closed
- auto-install prerequisite varsa installer SHA-256 + Authenticode valid
- auto-install prerequisite post-install re-probe basarili
- CI release artifacti uretiyor

## Sonraki Proje

Her repo migration'i ayri PR olarak yapilmalidir.

TurkuazInstaller Core'a proje-ozel logic eklenmemelidir.

Proje-ozel davranis manifest, trust provisioning, release pipeline veya ilgili uygulama reposunda kalmalidir.
