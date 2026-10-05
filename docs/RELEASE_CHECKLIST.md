# 📄 Dosya Yolu: /docs/RELEASE_CHECKLIST.md
# 📌 Amac: TurkuazInstaller Stable Community release oncesi zorunlu teknik ve operasyonel kontrolleri siralar
# 📌 Modul - Markdown
# Version: 1.0.2
# Aciklama: CI, reproducibility, immutable OIDC, signing preflight, attestation, recovery, security ve release asset kontrollerini tek checklistte toplar
# Bagimli Oldugu Katman: Tool | Config | View

# Stable Release Checklist

Release oncesi:

- [x] main Core CI yesil
- [x] main Contract Validation yesil
- [x] main Windows Desktop CI yesil
- [x] main Stable Readiness CI yesil
- [x] iki temiz publish agaci SHA-256 olarak birebir
- [x] deterministic release ZIP kalite kapisi yesil
- [x] production signing bootstrap tooling repository icinde
- [x] immutable OIDC subject ve production environment modeli tanimli
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
- [ ] release ZIP GitHub artifact attestation olustu
- [x] recovery testleri yesil
- [x] security review acik P1/P2 bulgu icermiyor
- [ ] release notes olustu
- [ ] release assetleri indirilebilir

Bu checklist tamamlanmadan stable tag release tamamlanmis kabul edilmez.
