# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim ve gelistirme giris dokumani
# 📌 Modul - Markdown
# Version: 0.5.0
# Aciklama: Tamamlanan Core, Provider ve Package Engine fazlarini ve sonraki Windows Bootstrap hedefini tanimlar

Bagimli Oldugu Katman: View

# TurkuazInstaller

TurkuazInstaller, TurkuazLabs masaustu uygulamalari icin ortak kurulum ve guncelleme platformudur.

## Ana hedefler

- Modern WinUI 3 kullanici arayuzu
- Kucuk ve bagimsiz native bootstrapper
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

Domain ve Application katmanlari WinUI, GitHub, Gitea, Velopack veya dosya sistemi gibi dis teknolojileri bilmez.

## Edition modeli

Community temel guvenlik, install/update, repair ve rollback ozelliklerini eksiksiz tasir.

Pro; private feed, lisans ve cihaz yetkilendirme, staged rollout, merkezi yonetim, analytics ve enterprise deployment gibi operasyonel moduller ekler.

## Durum

Aktif gelistirme: v0.5.0 Package Engine tamamlandi.

Tamamlanan bu faz:

- typed `PackageStage`
- atomik verified artifact staging
- Velopack Setup.exe initial install adapteri
- Velopack Update.exe update apply adapteri
- full nupkg repair ve rollback akislari
- shell kullanmayan `IProcessRunner` portu
- path containment ve preserve policy kontrolleri
- typed Package Engine hatalari
- Package Engine contract testleri

Sonraki roadmap adimi: v0.6.0 Windows Bootstrap.

Velopack adapter kararlari `docs/VELOPACK_ADAPTER.md` icinde tutulur.
