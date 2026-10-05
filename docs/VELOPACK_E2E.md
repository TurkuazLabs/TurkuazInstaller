# 📄 Dosya Yolu: /docs/VELOPACK_E2E.md
# 📌 Amac: TurkuazInstaller gercek Velopack E2E kalite kapisinin kapsamini ve failure anlamini dokumante etmek
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: vpk ile gercek Setup/full package uretimini ve Package Engine install-update-repair-rollback-uninstall test zincirini tanimlar
# Bagimli Oldugu Katman: Tool | Config

# Real Velopack E2E

Bu kalite kapisi FakeProcessRunner kullanmaz.

Windows hosted runner icinde pinlenmis vpk toolu ile iki gercek Velopack release uretilir.

Akis:

1. minimal Windows fixture publish edilir
2. v1 Setup.exe ve full nupkg uretilir
3. v2 full nupkg uretilir
4. TurkuazInstaller VelopackPackageEngine v1 Setup.exe ile install yapar
5. disk marker v1 olarak dogrulanir
6. v2 full nupkg ile update uygulanir
7. disk marker v2 olarak dogrulanir
8. v2 full nupkg ile repair uygulanir
9. disk marker v2 olarak tekrar dogrulanir
10. v1 full nupkg ile rollback uygulanir
11. disk marker v1 olarak dogrulanir
12. gercek Update.exe uninstall calistirilir
13. active install dosyalarinin kaldirildigi dogrulanir

vpk surumu config/velopack-e2e.psd1 icinde pinlenir.

Bu workflow yesil olmadan Stable Community runtime gercek Velopack entegrasyonu tamamlanmis kabul edilmez.
