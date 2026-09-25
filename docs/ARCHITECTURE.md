# 📄 Dosya Yolu: /docs/ARCHITECTURE.md
# 📌 Amac: TurkuazInstaller mimarisinin katmanlarini ve bagimlilik yonunu tanimlar
# 📌 Modul - Markdown
# Version: 0.1.0
# Aciklama: Clean Architecture, MVVM ve Ports/Adapters kararlarini proje hafizasina yazar

Bagimli Oldugu Katman: Service

# Architecture

## Ana akis

```text
WinUI View
    |
ViewModel
    |
Application Use Case
    |
Contracts / Ports
    |
Infrastructure / Provider Adapters

Domain tum akisin merkezindedir.
```

## Domain

Saf is kavramlarini tasir. WinUI, HTTP, GitHub, Velopack, registry veya dosya sistemi bilmez.

## Contracts

Application katmaninin dis dunya ile konusmak icin kullandigi portlari tanimlar.

## Application

Install, update, repair, rollback ve uninstall senaryolarini use-case seviyesinde yonetir.

## Infrastructure

HTTP, YAML, Windows API, release source ve package provider implementasyonlarini tasir.

## UI

WinUI 3 + MVVM katmanidir. Is kurali tasimaz.

## Provider modeli

Ilk portlar:

- IReleaseSource
- IPackageDownloader
- IPackageVerifier
- IPackageInstaller
- ITextCatalog

GitHub, Gitea, HTTP, Velopack, MSIX ve MSI adapterlari bu kontratlari uygular.

## Bootstrapper

Bootstrapper ana WinUI runtime'inin parcasi degildir. Kucuk, native ve bagimsiz executable olarak gelistirilecektir.

## Yasaklar

- Domain icinden Infrastructure referansi
- Application icinden WinUI veya Velopack referansi
- ViewModel icinde dosya, HTTP veya install mantigi
- Providera ozel davranisin Core icine gomulmesi
- Community guvenlik ozelliklerinin Pro lisansina baglanmasi
