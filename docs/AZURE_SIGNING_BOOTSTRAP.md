# 📄 Dosya Yolu: /docs/AZURE_SIGNING_BOOTSTRAP.md
# 📌 Amac: TurkuazInstaller production code-signing altyapisinin Azure ve GitHub tarafinda nasil kurulacagini aciklamak
# 📌 Modul - Markdown
# Version: 1.4.0
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

**2026-10-21 guncel kaynak kontrolu:** Microsoft Public Trust listesinde
ABD, Kanada, AB, Birlesik Krallik, Avustralya, Yeni Zelanda, Japonya,
Guney Kore, Singapur, Isvicre, Norvec ve Israil'deki kuruluslar yer
alir; bireysel gelistiriciler ABD/Kanada ile sinirlidir. Turkiye
bu listede degildir. Kayitli **yasal kurulusun** gercek ikamet/kayit ulkesi
dogrulanmadan bolge uygunlugu varsayilmaz. Bir Azure kaynak bolgesi secmek
bu yasal kisitlamayi asmaz.

Resmi kaynaklar:
- https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart
- https://learn.microsoft.com/en-us/azure/artifact-signing/concept-trust-models
- https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options

**Alternatif yol:** Azure Public Trust uygun degilse, genel Windows dagitimi
icin guvenilir CA'den organization-validated (OV) code-signing sertifikasi,
private key/HSM yonetimi ve RFC3161 timestamp politikasi ayri tasarlanmalidir.
Mevcut `.github/workflows/release.yml` Azure Artifact Signing action
kullaniyor; OV icin **yeni, ayri gozden gecirilmis bir signing adapter/workflow**
gerekir. Yalnizca sertifika satin almak mevcut Azure-only workflow'u
otomatik uyumlu hale getirmez. Private key/PFX sifresi Git, log veya
artifact icine konmaz; release ancak exact-head imza, yayinci, time stamp
ve Win10/11 kabul testlerinden sonra onaylanir.

Public Trust ve OV imzalari da SmartScreen download reputation veya antivirus
temiz sonucunu kendiliginden garanti etmez.

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

## Stable tag commit kaynagi (main ancestry gate)

`.github/workflows/release.yml` tag checkout'unda `fetch-depth: 0`
ile git gecmisini alir. Imzalamadan ve Azure girisinden once
`tools/Assert-ReleaseCommitOnMain.ps1`, `github.sha` kimligini
gercek checkout `HEAD` ile birebir karsilastirir; daha sonra bu
commitin `refs/remotes/origin/main` tarihcesinde bulundugunu dogrular.
Ref yoksa, tarihce eksikse, commit ayrismissa veya HEAD farkliysa
**fail-closed** davranir.

Ayrismis branch'te yanlislikla `v*.*.*` etiketi olusturulmasina
karsi ek bir guvenlik siniridir. Bu kontrol, **kotu niyetli birinin
etkin release workflow dosyasini veya tag'i degistirebildigi durumda
tek basina bir guven siniri degildir**: protected tags, production
environment approval ve yayin yetkileri ayrica korunmalidir.

`tests/scripts/Test-ReleaseCommitOnMain.ps1` bagimsiz gecici
git deposunda main, ayrismis branch, yanlis HEAD ve eksik remote
ref senaryolarini test eder. Basarili test gercek Azure signing
veya production rollout kabulune denk degildir.

## Zorunlu signed combined layout kabul kapisi

Signed Release workflow, hem `win-x64` hem de `win-arm64` combined
dagitiminda asagidaki **uc dosyanin tumunu** zorunlu tutar:

- `TurkuazInstaller.Bootstrapper.exe`
- `app/TurkuazInstaller.WinUI.exe`
- `cli/TurkuazInstaller.Cli.exe`

`tools/Assert-AuthenticodeRelease.ps1 -RequireCombinedLayout -ExpectedArchitecture x64|arm64`
eksik, bos, reparse-point ya da hedef mimariye uymayan PE EXE'yi
**SignTool verification calismadan once reddeder**. Mimari, PE basligindaki
makine koduyla dogrulanir: x64=0x8664, ARM64=0xAA64. Bu denetim
Azure signing sonrasinda fakat **release assetleri olusturulmadan once**
yapilir; yanlis mimarideki bir EXE'nin imzali olmasi yayin icin yeterli degildir.
Sonrasinda bulunan TurkuazInstaller EXE/DLL dosyalari mevcut SignTool
Authenticode denetiminden gecirilir. Imza probe workflow'u bu parametreyi
kullanmaz, cunku probe gercek combined dagitim degildir.

`tests/scripts/Test-AuthenticodeReleaseGuard.ps1` ve Signing Tooling
Validation Windows isi eksik/bos gecersiz EXE senaryolarini inceler.
Basarili regresyon testi **public-trust imzasi**, dogru legal yayinci
kimligi, Windows SmartScreen itibari veya antivirus temiz raporu **degildir**.
Gercek production sertifikasi/publisher bilgisi bagimsiz onaylanmadan
ve Azure signing preflight gercekten gecmeden release cikarma.
Kullaniciya ait antivirus olaylari ayrica [Issue #58](https://github.com/TurkuazLabs/TurkuazInstaller/issues/58)
ve `docs/ANTIVIRUS_EVIDENCE_LAB.md` uzerinden takip edilir.

## Release

Preflight yesil olduktan sonra:

1. main Stable Readiness yesil olmali
2. Directory.Build.props Version 1.0.0 olmali
3. v1.0.0 tag'i olusturulmali
4. Signed Release workflow production environment uzerinden calismali
5. imzali release ZIP, SHA256SUMS ve GitHub attestation yayinlanmali
