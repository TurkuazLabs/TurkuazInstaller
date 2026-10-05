# 📄 Dosya Yolu: /docs/RELEASE_CHECKLIST.md
# 📌 Amac: TurkuazInstaller Stable Community release oncesi zorunlu teknik ve operasyonel kontrolleri siralar
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: CI, reproducibility, signing, recovery, security ve release asset kontrollerini tek checklistte toplar
# Bagimli Oldugu Katman: Tool | Config | View

# Stable Release Checklist

Release oncesi:

- [ ] main Core CI yesil
- [ ] main Contract Validation yesil
- [ ] main Windows Desktop CI yesil
- [ ] Stable Readiness CI yesil
- [ ] urun surumu ile tag birebir eslesiyor
- [ ] Azure OIDC federated identity aktif
- [ ] Artifact Signing certificate profile aktif
- [ ] production signer minimum rol ile sinirli
- [ ] release EXE ve DLL Authenticode verification basarili
- [ ] SHA256SUMS.txt olustu
- [ ] recovery testleri yesil
- [ ] security review acik P1/P2 bulgu icermiyor
- [ ] release notes olustu
- [ ] release assetleri indirilebilir

Bu checklist tamamlanmadan stable tag release tamamlanmis kabul edilmez.
