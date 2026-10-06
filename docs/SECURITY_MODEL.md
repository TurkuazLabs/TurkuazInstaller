# 📄 Dosya Yolu: /docs/SECURITY_MODEL.md
# 📌 Amac: TurkuazInstaller Community install, update, repair, rollback ve uninstall guvenlik invariantlarini tanimlamak
# 📌 Modul - Markdown
# Version: 1.5.0
# Aciklama: Detached trust, private provider credentials, prerequisite/reboot resume, bootstrap self-update, path, process, state ve rollback kurallarini sabitler
# Bagimli Oldugu Katman: Service | Repo | Tool | Config

# Security Model

## Zorunlu Baseline

Community baseline:

- detached CMS/PKCS#7 manifest signature
- package-scoped external manifest publisher subject pinning
- zorunlu manifest signer certificate SHA-256 pinning
- SHA-256 artifact dogrulamasi
- manifest signature deklarasyonu varsa Authenticode trust dogrulamasi
- Authenticode publisher subject pinning
- optional artifact certificate SHA-256 pinning
- package-scoped cross-process operation lock
- crash operation journal
- HTTPS remote transport
- fail-closed generic prerequisite detector registry
- prerequisite auto-install icin zorunlu SHA-256 + Authenticode
- prerequisite install sonrasi zorunlu re-probe
- path traversal engelleme
- atomic staging
- shell-free process invocation
- apply sonrasi state commit
- rollback policy kontrolu
- uninstall basarisi sonrasi state delete
- secret degerlerin manifest icine yazilmamasi
- bootstrap self-update icin GitHub SHA-256 asset digest + current-signer Authenticode identity pinning
- private GitHub/Gitea tokenlari icin Windows Credential Manager + host-scoped Authorization

## Manifest Trust

Manifest YAML parsera girmeden once:

1. raw manifest byte'lari indirilir veya local dosyadan okunur
2. ayni kaynak icin `.p7s` detached CMS signature zorunlu tutulur
3. requested package id ile external trust store policy resolve edilir
4. CMS signature kriptografik olarak dogrulanir
5. signer sayisinin tam olarak bir oldugu dogrulanir
6. signer certificate validity period kontrol edilir
7. signer subject external policy ile birebir eslestirilir
8. signer certificate SHA-256 external pin ile birebir eslestirilir
9. ancak bundan sonra manifest strict UTF-8 olarak parse edilir

Trust policy manifestin kendisinden okunmaz.

Default desktop trust store:

```text
%LOCALAPPDATA%/TurkuazInstaller/config/manifest-trust.yml
```

Package trust entry yoksa manifest reddedilir.

Manifest trust certificate SHA-256 pini zorunludur.

## Artifact Verification

Apply oncesi:

1. size kontrol edilir
2. SHA-256 hesaplanir
3. digest manifest ile karsilastirilir
4. signature deklarasyonu varsa Windows WinVerifyTrust calisir
5. signer certificate subject manifest publisher_subject ile birebir eslesir
6. certificate_sha256 tanimliysa signer certificate SHA-256 pini birebir eslesir
7. verification basarisizsa stage/apply baslamaz

Signature deklarasyonu olmayan artifact SHA-256 baseline ile calisir.

## Prerequisites

Community v1.1 built-in prerequisite id degerleri:

- windows-build
- architecture
- dotnet-desktop-runtime

Detection Application katmanindaki detector registry uzerinden yapilir.

Her detector tek prerequisite id sahibidir.

Bilinmeyen prerequisite false kabul edilir ve operasyon durur.

Eksik prerequisite manifestte auto-install policy tasiyorsa:

1. installer artifact HTTPS veya file URI uzerinden alinir
2. artifact size ve SHA-256 dogrulanir
3. Authenticode signature zorunlu tutulur
4. publisher_subject birebir eslestirilir
5. certificate_sha256 varsa certificate pin birebir eslestirilir
6. installer dosyasi direct .exe degilse reddedilir
7. installer shell-free argument listesi ile calistirilir
8. elevation yalniz requires_elevation=true ise explicit UAC ile istenir
9. installer exit code 0 olmadan basarili kabul edilmez
10. kurulumdan sonra ayni detector zorunlu olarak yeniden kosar
11. requirement hala saglanmiyorsa ana package stage/apply baslamaz

Prerequisite auto-install artifacti unsigned calistirilamaz.

Exit code 1641 veya 3010 typed reboot sonucu olarak ele alinir.

Prerequisite process baslamadan once resume request kalici yazilmis, HKCU RunOnce bootstrap relaunch kaydi olusturulmus ve journal RebootResumeArmed checkpointine alinmis olmalidir.

Workflow RebootResumeArmed/AwaitingReboot checkpointini ve pending prerequisite id degerini kalici tutar, failure yazmaz ve package apply adimini reboot sonrasindaki resume akisi tamamlanana kadar baslatmaz.

Resume sirasinda journal/request package, operation ve version degerleri eslesmelidir. Signed manifest yeniden dogrulanir; resolved version ve package artifact SHA-256 persisted expected identity ile eslesmezse operasyon durur.

Pending prerequisite reboot sonrasinda hala saglanmiyorsa ayni installer tekrar calistirilmaz.

AwaitingReboot durumundaki package icin normal manual mutation baslatilamaz.

Detay: docs/REBOOT_RESUME.md

## Bootstrap Self Update

Automatic bootstrap self-update yalniz current bootstrap Authenticode trust kontrolunden geciyorsa etkinlesir.

Discovery GitHub latest stable release endpointinden exact TurkuazInstaller.Bootstrapper.exe assetini arar.

Daha yeni release icin:

1. release tag SemVer olarak current versiondan buyuk olmalidir
2. asset URL github.com HTTPS olmalidir
3. asset state uploaded olmalidir
4. expected size zorunludur
5. GitHub asset digest sha256 formatinda zorunludur
6. download byte count exact size ile eslesmelidir
7. downloaded SHA-256 expected digest ile birebir eslesmelidir
8. replacement WinVerifyTrust kontrolunden gecmelidir
9. replacement publisher subject current bootstrap ile ayni olmalidir
10. replacement certificate SHA-256 current bootstrap ile birebir ayni olmalidir
11. ancak bundan sonra two-process handoff baslatilir

Network discovery availability hatasi current trusted bootstrap ile normal startup'i engellemez.

Basarili GitHub response daha yeni release ilan ettigi halde required asset/digest metadata bozuksa update fail-closed durur.

Internal explicit replacement path ve complete-self-update source executable ayni signer verification zincirini atlayamaz.

Detay: docs/BOOTSTRAP_SELF_UPDATE.md

## Private Provider Credentials

Private GitHub/Gitea tokenlari manifest veya public config icinde tutulmaz.

Windows Credential Manager Generic Credential target formati:

`TurkuazInstaller/provider/{provider}/{authority}`

Provider credential resolver credential bulamazsa public/anonymous davranisa geri doner.

Credential varsa:

- GitHub Authorization scheme Bearer
- Gitea Authorization scheme token
- header yalniz HTTPS ve explicit authority allow-list eslesmesinde eklenir
- release metadata icindeki arbitrary asset host allow-liste otomatik eklenmez
- manifest ve detached .p7s requestleri ayni host-scoped policy'den gecer
- token query string, manifest veya log icine yazilmaz
- URI userinfo credential authority/origin olarak reddedilir

Transport authentication, signed manifest trust zincirini gevsetmez.

Private manifest yine CMS/PKCS#7, publisher subject ve certificate SHA-256 kontrollerinden gecmeden parse edilmez.

Detay: docs/PRIVATE_PROVIDER_CREDENTIALS.md

## Preserve Data

preserve_paths:

- install rootuna gore relative olmalidir
- target root disina cikamaz
- Velopack current altinda olamaz

## Rollback

Rollback:

- current manifest rollback.supported=true olmadan baslamaz
- previous manifest de ayni detached trust pipeline'indan gecer
- previous artifact ayni integrity/signature pipeline'indan gecer
- staged package id ve version previous release ile eslesir
- engine basarisizsa mevcut state korunur

## Concurrent Operations

Ayni package id icin ikinci install/update/repair/rollback/uninstall islemi cross-process file lock ile fail-fast reddedilir.

Farkli package id degerleri paralel calisabilir.

## Crash Journal

Her mutasyon operation'i package bazli journal checkpointleri yazar.

Checkpoint ornekleri:

- started
- validating prerequisites
- downloading
- verifying
- staging
- applying
- saving/removing state
- failed/cancelled

Basarili operation tamamlandiginda journal temizlenir.

Failed veya crash sonrasi kalan journal, diagnostic/recovery bilgisi olarak korunur.

## Uninstall

Uninstall installed state uzerinden hedef rootu bulur.

Velopack uninstall non-zero exit ile biterse state korunur.

State yalniz successful package engine sonucundan sonra silinir.

## Secret Boundary

Manifest veya public config private signing key, access token, private feed credential veya production certificate private key tasimaz.

Authentication secret Adapter/credential store sinirinda kalir.

Manifest trust store yalniz public certificate identity ve SHA-256 pin tasir; private key tasimaz.
