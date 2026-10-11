# 📄 Dosya Yolu: /docs/VELOPACK_E2E.md
# 📌 Amac: TurkuazInstaller gercek Velopack E2E kalite kapisinin kapsamini ve failure anlamini dokumante etmek
# 📌 Modul - Markdown
# Version: 1.1.0
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
6. current disindaki UserData klasorunde benzersiz veri marker'i olusturulur
7. v2 full nupkg ile update uygulanir; disk marker v2 ve UserData byte'lari dogrulanir
8. v2 full nupkg ile repair uygulanir; disk marker v2 ve UserData byte'lari dogrulanir
9. v1 full nupkg ile rollback uygulanir; disk marker v1 ve UserData byte'lari dogrulanir
10. gercek Update.exe uninstall calistirilir
11. active install dosyalarinin kaldirildigi dogrulanir

UserData fixture klasor/marker adlari config/velopack-e2e.psd1'dedir.
Bu test gercek Velopack package engine'i kapsar, signed manifest/workflow
oncesindeki trust verification zincirini calistirmaz. Uninstall sonrasi
user data muhafazasini da kanitlamaz; bu ayri urun-policy VM kabuludur.
Unsigned fixture hicbir zaman production release ya da guvenilir artifact
olarak sunulmaz.

vpk surumu config/velopack-e2e.psd1 icinde pinlenir.

Bu workflow yesil olmadan Stable Community runtime gercek Velopack entegrasyonu tamamlanmis kabul edilmez.
