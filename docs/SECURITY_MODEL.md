# 📄 Dosya Yolu: /docs/SECURITY_MODEL.md
# 📌 Amac: TurkuazInstaller Community install, update, repair, rollback ve uninstall guvenlik invariantlarini tanimlamak
# 📌 Modul - Markdown
# Version: 1.1.1
# Aciklama: Detached manifest trust, hash, Authenticode, prerequisite, path, staging, process, state ve rollback guvenlik kurallarini sabitler
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
- fail-closed prerequisite kontrolu
- path traversal engelleme
- atomic staging
- shell-free process invocation
- apply sonrasi state commit
- rollback policy kontrolu
- uninstall basarisi sonrasi state delete
- secret degerlerin manifest icine yazilmamasi

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

Stable v1 built-in prerequisite id degerleri:

- windows-build
- architecture
- dotnet-desktop-runtime

Bilinmeyen prerequisite false kabul edilir ve operasyon durur.

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
