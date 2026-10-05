# 📄 Dosya Yolu: /README.md
# 📌 Amac: TurkuazInstaller projesinin ana tanitim, kullanim ve release durumu giris dokumani
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Stable Community kod kapsamlarini ve production signing release gate durumunu ozetler

Bagimli Oldugu Katman: View

# TurkuazInstaller

TurkuazInstaller, TurkuazLabs masaustu uygulamalari icin ortak kurulum ve guncelleme platformudur.

## Ana hedefler

- Modern WinUI 3 kullanici arayuzu
- .NET runtime gerektirmeyen NativeAOT Windows bootstrapper
- Install, update, repair ve rollback akislari
- GitHub, Gitea, generic HTTPS ve local file release provider modeli
- Paket motorundan bagimsiz Core
- Velopack Package Engine adapteri
- SHA-256 artifact dogrulamasi
- Stable ve beta release kanallari
- Community ve Pro katmanlarinin ayni Core kontratlarini kullanmasi
- GUI ve CLI tarafinda ayni Application use-case katmaninin kullanilabilmesi

## Mimari

View -> Controller -> Presentation Service -> Application Use Case -> Port -> Adapter

Application Use Case -> Domain

Windows platform detaylari ayri TurkuazInstaller.Platform.Windows projesinde tutulur.

## Stable Community Durumu

Kod hedefi: v1.0.0 Stable Community.

Tamamlanan release kalite katmanlari:

- Core ve Windows CI
- contract validation
- iki publish agaci SHA-256 reproducibility dogrulamasi
- install ve rollback recovery testleri
- security review
- OIDC tabanli Azure Artifact Signing release workflow
- SignTool Authenticode verification
- signing sonrasi SHA-256 release checksum manifesti
- user ve release dokumani

Production v1.0.0 tag'i, Azure Artifact Signing identity ve certificate profile repository'ye baglanmadan olusturulmamalidir.

Unsigned production fallback yoktur.

## Dokuman

- docs/ARCHITECTURE.md
- docs/SECURITY_MODEL.md
- docs/SECURITY_REVIEW_v1.0.0.md
- docs/RELEASE_SIGNING.md
- docs/RELEASE_CHECKLIST.md
- docs/WINDOWS_BOOTSTRAP.md
- docs/WINUI_DESKTOP.md
- docs/USER_GUIDE.md
