# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim, kullanim ve release durumu giris dokumani
# 📌 Modul - Markdown
# Version: 1.0.3
# Aciklama: Stable Community kod tamamlanma durumunu, combined distribution modelini ve production signing dis bagimliligini ozetler

Bagimli Oldugu Katman: View

# TurkuazInstaller

TurkuazInstaller, TurkuazLabs masaustu uygulamalari icin ortak kurulum, guncelleme, onarma, rollback ve kaldirma platformudur.

## Ana hedefler

- Modern WinUI 3 kullanici arayuzu
- .NET runtime gerektirmeyen NativeAOT Windows bootstrapper
- Install, update, repair, rollback ve uninstall akislari
- GitHub, Gitea, generic HTTPS ve local file release provider modeli
- Paket motorundan bagimsiz Core
- Velopack Package Engine adapteri
- SHA-256 artifact dogrulamasi
- optional Authenticode artifact verification
- Windows prerequisite kontrolu
- Stable ve beta release kanallari
- Community ve Pro katmanlarinin ayni Core kontratlarini kullanmasi

## Mimari

View -> Controller -> Presentation Service -> Application Use Case -> Port -> Adapter

Application Use Case -> Domain

Windows platform detaylari ayri TurkuazInstaller.Platform.Windows projesinde tutulur.

## Stable Community Durumu

v1.0.0 Community kod gelistirmesi tamamlanmistir.

Main branch kalite kapilari:

- Core CI: yesil
- Contract Validation: yesil
- Windows Desktop CI: yesil
- Stable Readiness CI: yesil
- Signing Tooling Validation: yesil
- real Velopack E2E: yesil

Gercek Velopack E2E su zinciri Windows runner uzerinde calistirir:

- Setup.exe install
- full nupkg update
- repair
- rollback
- Update.exe uninstall

## Combined Distribution

Stable v1 release target win-x64 olarak sabitlenmistir.

Dagitim yapisi:

```text
TurkuazInstaller.Bootstrapper.exe
app/
  TurkuazInstaller.WinUI.exe
  ...
```

Kullanici TurkuazInstaller.Bootstrapper.exe calistirir.

Bootstrap prerequisite ve self-update handoff kontrollerinden sonra app/TurkuazInstaller.WinUI.exe dosyasini baslatir.

## Production Release Durumu

Kod ve release workflow gelistirmesi tamamlanmistir.

Production v1.0.0 tag'i su dis konfigurasyon tamamlanmadan olusturulmamalidir:

- Azure Artifact Signing account
- identity validation
- production certificate profile
- OIDC federated identity
- minimum signer RBAC
- GitHub production environment secret/variable degerleri

Unsigned production fallback yoktur.

Bu hesap/kayit islemleri daha sonra yapilabilir; uygulama gelistirmesinin blocker'i degildir.

## NSIS Yerine Kullanma

Yeni proje entegrasyon standardi:

docs/PROJECT_INTEGRATION.md

Manifest sablonu:

examples/project-manifest.template.yml

Bir proje TurkuazInstaller'a gecmeden once install, update, repair, rollback ve uninstall smoke testlerini gecmelidir.

## Dokuman

- docs/ARCHITECTURE.md
- docs/SECURITY_MODEL.md
- docs/SECURITY_REVIEW_v1.0.0.md
- docs/PROJECT_INTEGRATION.md
- docs/VELOPACK_E2E.md
- docs/AZURE_SIGNING_BOOTSTRAP.md
- docs/RELEASE_SIGNING.md
- docs/RELEASE_CHECKLIST.md
- docs/WINDOWS_BOOTSTRAP.md
- docs/WINUI_DESKTOP.md
- docs/USER_GUIDE.md
