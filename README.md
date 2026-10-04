# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim ve gelistirme giris dokumani
# 📌 Modul - Markdown
# Version: 0.6.0
# Aciklama: Tamamlanan Core, Provider, Package Engine ve Windows NativeAOT Bootstrap fazlarini ozetler

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
- SHA-256 ve dijital imza dogrulamasi
- Stable ve beta release kanallari
- Community ve Pro katmanlarinin ayni Core kontratlarini kullanmasi
- GUI ve CLI uzerinden ayni Application use-case katmaninin calismasi

## Mimari

```text
View -> ViewModel -> Application Use Case -> Port -> Adapter
                         |
                       Domain
```

Windows platform detaylari ayri `TurkuazInstaller.Platform.Windows` projesinde tutulur.

## Edition modeli

Community temel guvenlik, install/update, repair ve rollback ozelliklerini eksiksiz tasir.

Pro; private feed, lisans ve cihaz yetkilendirme, staged rollout, merkezi yonetim, analytics ve enterprise deployment gibi operasyonel moduller ekler.

## Durum

Aktif gelistirme: v0.6.0 Windows Bootstrap tamamlandi.

Tamamlanan bu faz:

- .NET 10 NativeAOT WinExe bootstrap
- Windows 10 1809 / build 17763 minimum prerequisite
- x64 ve Arm64 environment detection
- iki-process self-update handoff
- staged replacement cleanup
- explicit UAC elevation portu
- Windows platform adapter katmani
- gercek Windows runner uzerinde NativeAOT publish CI

Sonraki roadmap adimi: v0.7.0 WinUI 3.

Windows bootstrap kararlari `docs/WINDOWS_BOOTSTRAP.md` icinde tutulur.
