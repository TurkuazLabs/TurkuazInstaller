# 📄 Dosya Yolu: /docs/RELEASE_SIGNING.md
# 📌 Amac: TurkuazInstaller production release signing, deterministic packaging, attestation ve GitHub configuration adimlarini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.1
# Aciklama: Azure Artifact Signing OIDC identity, repository configuration, signed tag ve provenance akislarini dokumante eder
# Bagimli Oldugu Katman: Tool | Config

# Release Signing

TurkuazInstaller production release pipeline Azure Artifact Signing kullanir.

Private signing key repository, runner disk veya GitHub secret olarak saklanmaz.

## GitHub Secret Degerleri

Repository secret olarak:

- AZURE_CLIENT_ID
- AZURE_TENANT_ID
- AZURE_SUBSCRIPTION_ID

tanimlanir.

GitHub OIDC federated credential Azure App Registration tarafinda repository ve tag trust policy ile sinirlanmalidir.

## GitHub Variable Degerleri

Repository variable olarak:

- ARTIFACT_SIGNING_ENDPOINT
- ARTIFACT_SIGNING_ACCOUNT_NAME
- ARTIFACT_SIGNING_CERT_PROFILE_NAME
- ARTIFACT_SIGNING_TIMESTAMP_URL

tanimlanir.

Timestamp URL icin Microsoft Artifact Signing hesabiyla uyumlu RFC3161 endpoint kullanilir.

## Azure Rol

OIDC identity, ilgili Artifact Signing certificate profile icin minimum gerekli signer rolune sahip olmalidir.

Genel subscription owner veya contributor yetkisi verilmemelidir.

## Release Akisi

1. Directory.Build.props Version release surumune ayarlanir.
2. Stable Readiness CI yesil olmalidir.
3. v-Version tag'i olusturulur.
4. Signed Release workflow tag ile surum eslesmesini kontrol eder.
5. Signing configuration eksikse workflow fail olur.
6. Binary'ler publish edilir.
7. Azure OIDC login yapilir.
8. TurkuazInstaller EXE ve DLL dosyalari SHA-256 ile imzalanir.
9. RFC3161 SHA-256 timestamp uygulanir.
10. SignTool Authenticode verification yapar.
11. Imzalanmis publish klasorleri sabit ZIP metadata ile deterministic olarak paketlenir.
12. Ayni inputtan uretilen iki ZIP SHA-256 degeri birebir esit degilse release durur.
13. Signing sonrasinda SHA256SUMS.txt uretilir.
14. Release ZIP dosyalari GitHub artifact attestation ile provenance kaydi alir.
15. GitHub Release yayinlanir.

Unsigned production fallback yoktur.

## Reproducibility Siniri

Stable Readiness iki temiz unsigned publish agacini dosya listesi, boyut ve SHA-256 ile karsilastirir.

Release ZIP packager ayni input byte'larindan ayni ZIP byte'larini uretir.

RFC3161 timestamp zaman bilgisi tasidigi icin farkli production signing calistirmalarinin imzali binary byte'larinin birbirine esit olmasi beklenmez. Reproducibility iddiasi signing oncesi publish agaci ve ayni signed input icin deterministic archive katmanlariyla sinirlidir.

## Imza Dogrulama

Release binary dogrulamasinda Windows Default Authentication Policy kullanilir.

Release ZIP hash dogrulamasi icin GitHub Release icindeki SHA256SUMS.txt kullanilir.

GitHub provenance dogrulamasi icin:

gh attestation verify <artifact> -R TurkuazLabs/TurkuazInstaller
