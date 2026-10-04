# 📄 Dosya Yolu: /docs/VELOPACK_ADAPTER.md
# 📌 Amac: Merkezi TurkuazInstaller ile Velopack arasindaki adapter sinirini ve runtime kontratini tanimlamak
# 📌 Modul - Markdown
# Version: 0.5.0
# Aciklama: Setup.exe ve Update.exe CLI entegrasyonunu, staging guvenligini ve preserve path politikasini dokumante eder
# Bagimli Oldugu Katman: Service | Tool | Config

# Velopack Adapter

TurkuazInstaller merkezi bir installer oldugu icin baska uygulamalari kurar ve gunceller.

Bu nedenle uygulama-ici self-update modeli yerine Velopack tarafindan uretilen executable kontrati kullanilir:

- ilk kurulum icin `Setup.exe`
- mevcut kurulum update, repair ve rollback islemleri icin kurulum rootundaki `Update.exe`
- update, repair ve rollback artifacti olarak full `.nupkg`

## Staging

Package Engine yalniz daha once verification katmanindan gecmis artifacti kabul eder.

Stage akisi:

1. artifact dosyasinin varligi kontrol edilir
2. manifest artifact URI extension degerinden Velopack artifact turu belirlenir
3. staging root altinda benzersiz operasyon klasoru olusturulur
4. artifact once `.partial` dosyasina kopyalanir
5. ayni klasor icinde atomic rename ile final staged dosya olusturulur
6. typed `PackageStage` dondurulur

Generated path staging root disina cikarsa islem reddedilir.

## Initial Install

Setup artifact apply komutu:

```text
Setup.exe --silent --installto <target>
```

Argumentlar shell stringi olarak birlestirilmez. `ProcessStartInfo.ArgumentList` ile ayri argumentlar olarak calistirilir.

## Update

Full package apply komutu:

```text
Update.exe --silent --rootDir <target> --packageDir <target>/packages apply --norestart --package <full.nupkg>
```

Staged nupkg once install root altindaki `packages` klasorune atomic replace ile kopyalanir.

## Repair

Repair ayni release icin dogrulanmis full nupkg artifactini tekrar `Update.exe apply` akisi ile uygular.

Setup executable repair artifacti olarak kabul edilmez.

## Rollback

Rollback yalniz `RollbackPlan.PreviousRelease` ile birebir eslesen staged full nupkg artifactini kabul eder.

Yanlis package id veya yanlis version staged artifact reddedilir.

## Preserve Paths

Velopack aktif uygulama dosyalarini `current` klasorunde yonetir.

TurkuazInstaller Community preserve policy:

- preserve path install rootuna gore relative olmalidir
- path traversal yasaktir
- kalici uygulama verisi `current` disinda tutulmalidir
- `current` altindaki preserve path reddedilir
- adapter install rootundaki diger veri klasorlerini silmez

Bu kural update ve rollback sirasinda kalici veri ile executable payload sinirini net tutar.

## Process Boundary

Harici executable cagirilari `IProcessRunner` portu uzerinden yapilir.

Gercek adapter `SystemProcessRunner` kullanir. Unit testler fake process runner ile calisir ve gercek Setup.exe veya Update.exe baslatmaz.

Non-zero process exit code typed `PackageEngineException` olarak yukari tasinir.
