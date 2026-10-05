# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim ve gelistirme giris dokumani
# 📌 Modul - Markdown
# Version: 0.7.0
# Aciklama: Tamamlanan Core, Provider, Package Engine, Windows Bootstrap ve WinUI 3 fazlarini ozetler

Bagimli Oldugu Katman: View

# TurkuazInstaller

TurkuazInstaller, TurkuazLabs masaustu uygulamalari icin ortak kurulum ve guncelleme platformudur.

## Ana hedefler

- Modern WinUI 3 kullanici arayuzu
- .NET runtime gerektirmeyen NativeAOT Windows bootstrapper
- Install, update, repair, rollback ve uninstall akislari
- GitHub, Gitea, generic HTTPS ve local file release provider modeli
- Paket motorundan bagimsiz Core
- Velopack Package Engine adapteri
- SHA-256 artifact dogrulamasi
- Stable ve beta release kanallari
- Community ve Pro katmanlarinin ayni Core kontratlarini kullanmasi
- GUI ve CLI uzerinden ayni Application use-case katmaninin calismasi

## Mimari

```text
View -> Controller -> Presentation Service -> Application Use Case -> Port -> Adapter
                                                |
                                              Domain
```

Windows platform detaylari ayri `TurkuazInstaller.Platform.Windows` projesinde tutulur.

## Edition modeli

Community temel guvenlik, install/update, repair ve rollback ozelliklerini eksiksiz tasir.

Pro; private feed, lisans ve cihaz yetkilendirme, staged rollout, merkezi yonetim, analytics ve enterprise deployment gibi operasyonel moduller ekler.

## Durum

Aktif gelistirme: v0.7.0 WinUI 3 tamamlandi.

Tamamlanan masaustu fazi:

- Windows App SDK 2.5.1 stable / WinUI 3
- unpackaged self-contained desktop publish
- framework bagimsiz Presentation ViewModel katmani
- ince Controller -> Service request akisi
- install/update/repair/rollback ekran ve eventleri
- progress, cancel, retry ve error recovery UX
- gercek Application InstallerWorkflowService
- HTTPS/file artifact downloader
- SHA-256 ve size verification
- JSON installed-state repository
- mevcut Velopack Package Engine runtime baglantisi
- Windows CI WinUI artifact publish

Sonraki roadmap adimi: v1.0.0 Stable Community.

WinUI mimari kararlari `docs/WINUI_DESKTOP.md` icinde tutulur.
