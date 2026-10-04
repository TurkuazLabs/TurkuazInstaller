# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim ve gelistirme giris dokumani
# 📌 Modul - Markdown
# Version: 0.4.0
# Aciklama: Tamamlanan Core ve Provider fazlarini, Windows-first installer hedeflerini ve Community/Pro sinirini tanimlar

Bagimli Oldugu Katman: View

# TurkuazInstaller

TurkuazInstaller, TurkuazLabs masaustu uygulamalari icin ortak kurulum ve guncelleme platformudur.

## Ana hedefler

- Modern WinUI 3 kullanici arayuzu
- Kucuk ve bagimsiz native bootstrapper
- Install, update, repair, rollback ve uninstall akislari
- GitHub, Gitea, generic HTTPS ve local file release provider modeli
- Paket motorundan bagimsiz Core; Velopack ilk adapterlardan biri olacak
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

Pro; private feed, lisans/cihaz yetkilendirme, staged rollout, merkezi yonetim, analytics ve enterprise deployment gibi operasyonel moduller ekler.

## Durum

Aktif gelistirme: v0.4.0 Providers tamamlandi.

Tamamlanan provider fazi:

- ortak YAML installer manifest parseri
- GitHub Releases adapteri
- Gitea Releases adapteri
- generic HTTPS manifest adapteri
- local file / air-gapped adapteri
- HTTPS transport baseline
- package/channel request validation
- provider contract unit testleri

Sonraki roadmap adimi: v0.5.0 Package Engine.

Detayli kararlar `docs/` altinda tutulur.
