# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim, kullanim ve release durumu giris dokumani
# 📌 Modul - Markdown
# Version: 1.8.0
# Aciklama: Stable Community kod durumunu, x64/ARM64 dagitimlarini, signed trust, update policy ve production signing durumunu ozetler

Bagimli Oldugu Katman: View

# TurkuazInstaller

TurkuazInstaller, TurkuazLabs masaustu uygulamalari icin ortak kurulum, guncelleme, onarma, rollback ve kaldirma platformudur.

## Ana hedefler

- Modern WinUI 3 kullanici arayuzu
- .NET runtime gerektirmeyen NativeAOT Windows bootstrapper
- Install, update, repair, rollback ve uninstall akislari
- GitHub, Gitea, generic HTTPS ve local file release provider modeli
- Windows Credential Manager tabanli private GitHub/Gitea credentials
- ortak system/direct/custom HTTP proxy policy
- committed install state tabanli kurulu uygulama katalog/list UI
- signed manifest + installed state tabanli read-only update discovery UX
- session-only read-only background update policy
- package/channel bazli version skip ve maximum-version pinning
- signed manifest tabanli safe Windows shortcut + per-user URL protocol actions
- Paket motorundan bagimsiz Core
- Velopack Package Engine adapteri
- CMS/PKCS#7 detached manifest signature
- package-scoped external manifest publisher + certificate SHA-256 pinning
- SHA-256 artifact dogrulamasi
- Authenticode artifact verification + publisher pinning
- optional artifact certificate SHA-256 pinning
- package-scoped cross-process operation lock
- crash/reboot operation journal
- structured JSONL diagnostics
- generic prerequisite detector registry
- Windows CLI / silent automation runtime
- hash + Authenticode dogrulamali prerequisite auto-install
- Windows prerequisite kontrolu
- Stable ve beta release kanallari
- Community ve Pro katmanlarinin ayni Core kontratlarini kullanmasi

## Mimari

View -> Controller -> Presentation Service -> Application Use Case -> Port -> Adapter

Application Use Case -> Domain

Windows platform detaylari ayri TurkuazInstaller.Platform.Windows projesinde tutulur.

Manifest trust policy manifestin kendisinden okunmaz; package bazli external Repo uzerinden resolve edilir.

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

Stable Windows dagitimi native win-x64 ve win-arm64 hedefleri icin uretilir.

Her mimaride combined dagitim yapisi aynidir:

```text
TurkuazInstaller.Bootstrapper.exe
app/
  TurkuazInstaller.WinUI.exe
  ...
cli/
  TurkuazInstaller.Cli.exe
```

Release arsivleri:

- TurkuazInstaller-win-x64.zip
- TurkuazInstaller-win-arm64.zip

Kullanici TurkuazInstaller.Bootstrapper.exe calistirir.

Bootstrap prerequisite ve self-update handoff kontrollerinden sonra app/TurkuazInstaller.WinUI.exe dosyasini baslatir.

## v1.1 Replacement Readiness

Tamamlanan P1 maddeleri:

- Authenticode publisher pinning
- optional artifact certificate pinning
- concurrent package operation lock
- crash journal
- structured diagnostics
- detached signed manifest trust
- generic prerequisite detection engine
- secure prerequisite auto-install + post-install re-probe

Manifest runtime artik:

1. manifest raw byte'larini alir
2. `.p7s` CMS detached signature zorunlu tutar
3. package id ile external trust policy resolve eder
4. publisher subject ve certificate SHA-256 pinini dogrular
5. ancak sonra YAML parse eder

Kalan P1 maddeleri docs/ROADMAP.md icinde takip edilir.

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

Manifest trust sablonu:

examples/manifest-trust.template.yml

Manifest trust detaylari:

docs/MANIFEST_TRUST.md

Prerequisite engine detaylari:

docs/PREREQUISITES.md

Bir proje TurkuazInstaller'a gecmeden once signed manifest, install, update, repair, rollback ve uninstall smoke testlerini gecmelidir.

## Dokuman

- docs/ARCHITECTURE.md
- docs/SECURITY_MODEL.md
- docs/SECURITY_REVIEW_v1.0.0.md
- docs/PROJECT_INTEGRATION.md
- docs/MANIFEST_TRUST.md
- docs/PREREQUISITES.md
- docs/CLI.md
- docs/PRIVATE_PROVIDER_CREDENTIALS.md
- docs/PROXY.md
- docs/INSTALLED_APP_CATALOG.md
- docs/UPDATE_DISCOVERY.md
- docs/BACKGROUND_UPDATES.md
- docs/VERSION_POLICY.md
- docs/WINDOWS_INTEGRATION.md
- docs/VELOPACK_E2E.md
- docs/AZURE_SIGNING_BOOTSTRAP.md
- docs/RELEASE_SIGNING.md
- docs/RELEASE_CHECKLIST.md
- docs/WINDOWS_BOOTSTRAP.md
- docs/WINUI_DESKTOP.md
- docs/USER_GUIDE.md
