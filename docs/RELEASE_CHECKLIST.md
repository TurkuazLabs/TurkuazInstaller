# 📄 Dosya Yolu: /docs/RELEASE_CHECKLIST.md
# 📌 Amac: TurkuazInstaller Stable Community release oncesi zorunlu teknik ve operasyonel kontrolleri siralar
# 📌 Modul - Markdown
# Version: 1.1.0
# Aciklama: x64/ARM64 kod kalite kapilari ile Azure/GitHub production signing operasyonlarini ayri checklist gruplarinda takip eder
# Bagimli Oldugu Katman: Tool | Config | View

# Stable Release Checklist

## Kod ve Dagitim Kalitesi

- [x] main Core CI yesil
- [x] main Contract Validation yesil
- [x] main Windows Desktop CI yesil
- [x] main Stable Readiness CI yesil
- [x] main Velopack E2E yesil
- [x] main Signing Tooling Validation yesil
- [x] bootstrap orchestration testleri yesil
- [x] real install/update/repair/rollback/uninstall E2E yesil
- [x] win-x64 combined bootstrap + app/WinUI + CLI distribution olusuyor
- [x] win-arm64 combined bootstrap + app/WinUI + CLI distribution olusuyor
- [x] iki temiz publish agaci SHA-256 olarak birebir
- [x] deterministic win-x64 combined release ZIP kalite kapisi yesil
- [x] deterministic win-arm64 combined release ZIP kalite kapisi yesil
- [x] recovery testleri yesil
- [x] security review acik P1/P2 bulgu icermiyor
- [x] production signing bootstrap tooling repository icinde
- [x] immutable OIDC subject ve production environment modeli tanimli
- [x] project integration ve NSIS migration standardi tanimli

## Production Signing ve Yayin

Bu bolum hesap/kayit islemleridir ve kod gelistirmesinden ayri tutulur.

- [ ] Azure Artifact Signing account olusturuldu
- [ ] identity validation Azure Portal'da tamamlandi
- [ ] production certificate profile active
- [ ] OIDC federated identity Azure'a baglandi
- [ ] Artifact Signing Certificate Profile Signer minimum RBAC atandi
- [ ] GitHub production environment secrets/variables yazildi
- [ ] Production Signing Preflight yesil
- [ ] urun surumu ile tag birebir eslesiyor
- [ ] release EXE ve DLL Authenticode verification basarili
- [ ] SHA256SUMS.txt olustu
- [ ] win-x64 ve win-arm64 release ZIP GitHub artifact attestation olustu
- [ ] release notes olustu
- [ ] release assetleri indirilebilir

Production signing bolumu tamamlanmadan signed v1.0.0 GitHub Release yayinlanmis kabul edilmez.
