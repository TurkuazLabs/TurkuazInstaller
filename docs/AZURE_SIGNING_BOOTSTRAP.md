# 📄 Dosya Yolu: /docs/AZURE_SIGNING_BOOTSTRAP.md
# 📌 Amac: TurkuazInstaller production code-signing altyapisinin Azure ve GitHub tarafinda nasil kurulacagini aciklamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Otomatik bootstrap, zorunlu manuel identity validation, immutable OIDC ve preflight akislarini adim adim tanimlar
# Bagimli Oldugu Katman: Tool | Config

# Azure Signing Bootstrap

TurkuazInstaller production release hatti Azure Artifact Signing ve GitHub OIDC kullanir.

Private signing key GitHub secret, runner diski veya repository icinde tutulmaz.

## Neler Otomatik

tools/release/Initialize-ProductionSigning.ps1 su kaynaklari idempotent olarak hazirlar:

- Microsoft.CodeSigning resource provider registration
- artifact-signing Azure CLI extension
- Azure resource group
- Artifact Signing account
- Microsoft Entra application
- service principal
- GitHub production environment
- main branch ve v*.*.* tag deployment policy
- immutable GitHub OIDC subject
- Azure federated identity credential
- GitHub production environment secrets ve variables
- certificate profile, IdentityValidationId verildiginde
- Artifact Signing Certificate Profile Signer minimum RBAC rolu

## Zorunlu Manuel Adim

Artifact Signing identity validation Azure Portal icinde tamamlanmak zorundadir.

Azure CLI identity validation istegini tamamlayamaz.

Ilk bootstrap calistirmasindan sonra:

1. Azure Portal acin.
2. Artifact Signing account icine girin.
3. Identity validations bolumunde production kimlik dogrulamasini tamamlayin.
4. Tamamlanan kaydin Identity validation Id degerini kopyalayin.
5. Bootstrap scriptini IdentityValidationId ile tekrar calistirin.

## Public Trust Uygunlugu

Public GitHub release icin hedef profile type PublicTrust'tur.

Microsoft Public Trust identity/certificate kullanimini belirli ulke ve bolgelerdeki kuruluslarla sinirlar.

Yasal kurulus Public Trust desteklenen bolgelerden birinde degilse PrivateTrust, public Windows dagitimi icin ayni guven modelini saglamaz.

Bu durumda production release imzasi icin desteklenen bir public code-signing saglayicisi veya uygun bir yasal entity gerekir.

## Bootstrap

On kosullar:

- Azure CLI
- GitHub CLI
- az login tamamlanmis Azure oturumu
- gh auth login tamamlanmis GitHub oturumu
- Azure subscription icinde gerekli resource/RBAC olusturma yetkisi
- TurkuazInstaller repository Administration write yetkisi

Ilk asama ornegi:

pwsh ./tools/release/Initialize-ProductionSigning.ps1 -SubscriptionId "<subscription-guid>" -ResourceGroupName "rg-turkuaz-signing" -Location "westeurope" -AccountName "<globally-unique-account>" -ConfigureGitHub

Identity validation tamamlandiktan sonra:

pwsh ./tools/release/Initialize-ProductionSigning.ps1 -SubscriptionId "<subscription-guid>" -ResourceGroupName "rg-turkuaz-signing" -Location "westeurope" -AccountName "<globally-unique-account>" -IdentityValidationId "<identity-validation-guid>" -ConfigureGitHub

Script tekrar calistirilabilir.

Var olan kaynaklari yeniden olusturmak yerine kontrol eder ve eksik katmani tamamlar.

## Immutable GitHub OIDC

TurkuazInstaller repository 25 Eylul 2026 tarihinde olusturuldu.

Bu nedenle GitHub immutable OIDC subject modeli kullanilir.

Production subject:

repo:TurkuazLabs@326224284/TurkuazInstaller@1388165383:environment:production

Release workflow ve preflight workflow production environment kullandigi icin ayni federated credential ile authenticate olur.

Environment deployment policy yalniz:

- main branch
- v*.*.* tag

kaynaklarina izin verir.

## GitHub Production Environment

Bootstrap ConfigureGitHub ile:

- production environment olusturulur
- immutable OIDC subject zorlanir
- main branch policy eklenir
- v*.*.* tag policy eklenir
- AZURE_CLIENT_ID environment secret olarak yazilir
- AZURE_TENANT_ID environment secret olarak yazilir
- AZURE_SUBSCRIPTION_ID environment secret olarak yazilir
- ARTIFACT_SIGNING_ENDPOINT environment variable olarak yazilir
- ARTIFACT_SIGNING_ACCOUNT_NAME environment variable olarak yazilir
- ARTIFACT_SIGNING_CERT_PROFILE_NAME environment variable olarak yazilir
- ARTIFACT_SIGNING_TIMESTAMP_URL environment variable olarak yazilir

## Preflight

Stable tag olusturmadan once GitHub Actions icinden:

Production Signing Preflight

workflow'u main ref ile manuel calistirilir.

Preflight:

1. production environment policy uygular
2. GitHub OIDC token alir
3. Azure login yapar
4. kucuk TurkuazInstaller.SigningProbe PE/DLL uretir
5. Artifact Signing ile SHA-256 imzalar
6. RFC3161 timestamp uygular
7. SignTool ile Authenticode verification yapar

Bu workflow yesil olmadan v1.0.0 tag'i olusturulmaz.

## Release

Preflight yesil olduktan sonra:

1. main Stable Readiness yesil olmali
2. Directory.Build.props Version 1.0.0 olmali
3. v1.0.0 tag'i olusturulmali
4. Signed Release workflow production environment uzerinden calismali
5. imzali release ZIP, SHA256SUMS ve GitHub attestation yayinlanmali
