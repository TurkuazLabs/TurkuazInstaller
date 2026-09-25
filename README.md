# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim ve gelistirme giris dokumani
# 📌 Modul - Markdown
# Version: 0.1.0
# Aciklama: Windows-first kurulum, guncelleme, onarim ve rollback platformunun temel hedeflerini tanimlar

Bagimli Oldugu Katman: View

# TurkuazInstaller

TurkuazInstaller, TurkuazLabs masaustu uygulamalari icin ortak kurulum ve guncelleme platformudur.

## Teknik yon

- Windows-first
- .NET 10
- WinUI 3 + MVVM
- Clean Architecture
- Ports / Adapters
- Native bootstrapper
- Provider-independent Core
- Community + private Pro extension modeli

## Bagimlilik akisi

```text
View -> ViewModel -> Application Use Case -> Contracts / Ports -> Adapters
                           |
                         Domain
```

Domain; WinUI, HTTP, GitHub, Gitea, Velopack, MSI veya MSIX bilmez.

## Edition modeli

Community; temel guvenlik, install/update, repair, rollback, GUI ve CLI yeteneklerini tasir.

Pro; private feed, lisans/cihaz yetkilendirme, staged rollout, merkezi yonetim, analytics ve enterprise deployment gibi operasyon modulleri ekler.

## Aktif hedef

v0.1.0 Foundation.

Detayli mimari, roadmap ve guvenlik kararlari `docs/` altindadir.
