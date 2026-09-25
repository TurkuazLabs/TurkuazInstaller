# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim ve gelistirme giris dokumani
# 📌 Modul - Markdown
# Version: 0.1.0
# Aciklama: Windows-first kurulum, guncelleme, onarim ve rollback platformunun temel hedeflerini tanimlar

Bagimli Oldugu Katman: View

# TurkuazInstaller

TurkuazInstaller, TurkuazLabs masaustu uygulamalari icin ortak kurulum ve guncelleme platformudur.

## Ana hedefler

- Modern WinUI 3 kullanici arayuzu
- Kucuk ve bagimsiz native bootstrapper
- Install, update, repair, rollback ve uninstall akislari
- GitHub, Gitea ve HTTP release source provider modeli
- Paket motorundan bagimsiz Core; Velopack ilk adapterlardan biri olacak
- SHA-256 ve dijital imza dogrulamasi
- Stable, beta ve ileride staged rollout kanallari
- Community ve Pro katmanlarinin ayni Core kontratlarini kullanmasi
- GUI ve CLI uzerinden ayni Application use-case katmaninin calismasi

## Mimari

TurkuazInstaller klasik MVC kullanmaz.

```text
View -> ViewModel -> Application Use Case -> Port -> Adapter
                         |
                       Domain
```

Domain ve Application katmanlari WinUI, GitHub, Velopack veya dosya sistemi gibi dis teknolojileri bilmez.

## Edition modeli

Community temel guvenlik, install/update, repair ve rollback ozelliklerini eksiksiz tasir.

Pro; private feed, lisans/cihaz yetkilendirme, staged rollout, merkezi yonetim, analytics ve enterprise deployment gibi operasyonel moduller ekler.

## Durum

Aktif gelistirme: v0.1.0 Foundation.

Detayli kararlar `docs/` altinda tutulur.
