# 📄 Dosya Yolu: /docs/BACKGROUND_UPDATES.md
# 📌 Amac: TurkuazInstaller session background update policy konfigurasyonunu, schedule ve guvenlik sinirlarini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: background-updates.json, startup/periodic signed discovery, failure isolation ve no-auto-install politikasini aciklar
# Bagimli Oldugu Katman: Config | Service | Tool | View

# Background Update Policy

Background update policy WinUI uygulamasi acikken calisan read-only update discovery mekanizmasidir.

Windows service, scheduled task veya app kapaliyken calisan agent olusturmaz.

Config yolu:

`%LOCALAPPDATA%/TurkuazInstaller/config/background-updates.json`

Dosya yoksa policy kapali kabul edilir.

## Varsayilan

```json
{
  "schema_version": 1,
  "enabled": false,
  "interval_minutes": 60,
  "entries": []
}
```

Bu durumda network background check baslatilmaz.

## Etkin Policy

```json
{
  "schema_version": 1,
  "enabled": true,
  "interval_minutes": 30,
  "entries": [
    {
      "package_id": "example-app",
      "channel": "stable",
      "manifest_source": "https://updates.example.com/example/installer-manifest.yml"
    }
  ]
}
```

Interval:

- minimum 15 dakika
- maximum 1440 dakika
- default 60 dakika

En fazla 100 entry kabul edilir.

Ayni package id + channel cifti iki kez tanimlanamaz.

## Manifest Source

Her background entry mevcut signed manifest pipeline'ini kullanir.

Desteklenen kaynaklar:

- HTTPS manifest URL
- absolute local manifest dosya yolu

Persistent config icinde secret tasimamak icin HTTPS source:

- userinfo tasiyamaz
- query tasiyamaz
- fragment tasiyamaz

Private GitHub/Gitea credential gerekiyorsa secret background config'e yazilmaz; mevcut credential adapter siniri kullanilir.

## Schedule

Policy enabled ve entry listesi bos degilse:

1. normal WinUI startup/resume akisi tamamlanir
2. bir read-only background check cycle calisir
3. DispatcherQueueTimer configured interval ile periyodik cycle baslatir
4. window kapaninca timer durur ve aktif cycle cancellation alir

Cycle su durumlarda skip edilir:

- installer mutation aktif
- manual update discovery aktif
- onceki background cycle hala aktif

## Read-only Sinir

Background cycle yalniz IInstallerRuntimeService.CheckUpdateAsync kullanir.

Her entry:

1. signed manifest provider ile latest release resolve eder
2. committed installed state'i okur
3. version karsilastirmasi yapar
4. available/current/not-installed bilgisini sayar

Background cycle:

- package artifact indirmez
- prerequisite kurmaz
- stage/apply yapmaz
- install state yazmaz
- operation journal yazmaz
- reboot resume state yazmaz
- otomatik update baslatmaz

Bu tranche bildirim/policy katmanidir; mutation karari kullaniciya aittir.

## Failure Isolation

Bir entry provider/network/trust hatasi verirse diger entry'ler kontrol edilmeye devam eder.

Cycle sonunda WinUI durum satiri:

- basarili kontrol sayisi
- guncelleme bulunan kurulu paket sayisi
- hata sayisi

bilgisini gosterir.

Background failure ana installer operation error state'ini degistirmez.

## Concurrency

Background cycle aktifken manual mutation, manual update discovery ve catalog refresh baslatilmaz.

Manual/mutation akisi aktifken timer tick yeni background cycle baslatmaz.

Bu sinir ayni package/provider/state katmaninda paralel yarisi engeller.
