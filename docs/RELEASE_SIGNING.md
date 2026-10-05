# 📄 Dosya Yolu: /docs/RELEASE_SIGNING.md
# 📌 Amac: TurkuazInstaller production release signing, deterministic packaging, attestation ve GitHub configuration adimlarini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.2
# Aciklama: Azure Artifact Signing, immutable GitHub OIDC production environment, signed tag ve provenance akislarini dokumante eder
# Bagimli Oldugu Katman: Tool | Config

# Release Signing

TurkuazInstaller production release pipeline Azure Artifact Signing kullanir.

Private signing key repository, runner disk veya GitHub secret olarak saklanmaz.

## Production Environment

Release ve signing preflight job'lari GitHub production environment kullanir.

Environment deployment policy:

- main branch
- v*.*.* tag

ile sinirlidir.

TurkuazInstaller repository 25 Eylul 2026 tarihinde olusturuldugu icin GitHub immutable OIDC subject modeli kullanilir.

OIDC subject:

repo:TurkuazLabs@326224284/TurkuazInstaller@1388165383:environment:production

## GitHub Environment Secret Degerleri

production environment secret olarak:

- AZURE_CLIENT_ID
- AZURE_TENANT_ID
- AZURE_SUBSCRIPTION_ID

tanimlanir.

## GitHub Environment Variable Degerleri

production environment variable olarak:

- ARTIFACT_SIGNING_ENDPOINT
- ARTIFACT_SIGNING_ACCOUNT_NAME
- ARTIFACT_SIGNING_CERT_PROFILE_NAME
- ARTIFACT_SIGNING_TIMESTAMP_URL

tanimlanir.

## Azure Rol

OIDC service principal yalniz ilgili certificate profile scope'unda:

Artifact Signing Certificate Profile Signer

rolune sahip olmalidir.

Genel subscription owner veya contributor yetkisi release identity icin verilmemelidir.

## Bootstrap

Azure ve GitHub signing altyapisi:

tools/release/Initialize-ProductionSigning.ps1

ile hazirlanir.

Identity validation Azure Portal'da manuel tamamlanir.

Detayli akış docs/AZURE_SIGNING_BOOTSTRAP.md icindedir.

## Production Signing Preflight

v1.0.0 tag'i olusturmadan once Production Signing Preflight workflow'u main uzerinde manuel calistirilir.

Workflow gercek bir probe PE/DLL dosyasini imzalar ve Authenticode verification yapar.

Bu sayede su katmanlar tag olusturmadan test edilir:

- environment deployment policy
- immutable GitHub OIDC
- Azure federated identity
- Artifact Signing endpoint
- signing account
- certificate profile
- minimum signer RBAC
- RFC3161 timestamp
- SignTool verification

## Release Akisi

1. Directory.Build.props Version release surumune ayarlanir.
2. main Stable Readiness CI yesil olmalidir.
3. Production Signing Preflight yesil olmalidir.
4. v-Version tag'i olusturulur.
5. Signed Release workflow tag ile surum eslesmesini kontrol eder.
6. production environment policy uygulanir.
7. Signing configuration eksikse workflow fail olur.
8. Binary'ler publish edilir.
9. Azure OIDC login yapilir.
10. TurkuazInstaller EXE ve DLL dosyalari SHA-256 ile imzalanir.
11. RFC3161 SHA-256 timestamp uygulanir.
12. SignTool Authenticode verification yapar.
13. Imzalanmis publish klasorleri sabit ZIP metadata ile deterministic olarak paketlenir.
14. Ayni inputtan uretilen iki ZIP SHA-256 degeri birebir esit degilse release durur.
15. Signing sonrasinda SHA256SUMS.txt uretilir.
16. Release ZIP dosyalari GitHub artifact attestation ile provenance kaydi alir.
17. GitHub Release yayinlanir.

Unsigned production fallback yoktur.

## Reproducibility Siniri

Stable Readiness iki temiz unsigned publish agacini dosya listesi, boyut ve SHA-256 ile karsilastirir.

Release ZIP packager ayni input byte'larindan ayni ZIP byte'larini uretir.

RFC3161 timestamp zaman bilgisi tasidigi icin farkli production signing calistirmalarinin imzali binary byte'larinin birbirine esit olmasi beklenmez.

Reproducibility iddiasi signing oncesi publish agaci ve ayni signed input icin deterministic archive katmanlariyla sinirlidir.

## Imza Dogrulama

Release binary dogrulamasinda Windows Default Authentication Policy kullanilir.

Release ZIP hash dogrulamasi icin GitHub Release icindeki SHA256SUMS.txt kullanilir.

GitHub provenance dogrulamasi icin:

gh attestation verify <artifact> -R TurkuazLabs/TurkuazInstaller
