# 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/README.md
# 📌 Amac: Native bootstrapper alt projesinin sinirlarini ve v0.2.0 hedefini tanimlar
# 📌 Modul - Markdown
# Version: 0.1.0
# Aciklama: Bootstrapper kodu yazilmadan once sorumluluklarinin Core ve UI'dan ayrilmasini garanti eder

Bagimli Oldugu Katman: Tool

# Native Bootstrapper

Bu klasor v0.2.0 ile native Windows bootstrapper kodunu barindiracak.

Bootstrapper sorumluluklari:

- CPU mimarisi tespiti
- HTTPS bootstrap manifest alma
- Installer runtime package indirme
- SHA-256 ve imza dogrulama
- Runtime'i calistirma
- Minimal native hata UI

Bootstrapper ana WinUI installer runtime'ini veya Pro ozelliklerini icermeyecek.
