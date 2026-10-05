# 📄 Dosya Yolu: /docs/SECURITY_MODEL.md
# 📌 Amac: TurkuazInstaller Community install, update, repair, rollback ve uninstall guvenlik invariantlarini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Hash, optional Authenticode, prerequisite, path, staging, process, state ve rollback guvenlik kurallarini sabitler
# Bagimli Oldugu Katman: Service | Repo | Tool | Config

# Security Model

## Zorunlu Baseline

Community baseline:

- SHA-256 artifact dogrulamasi
- manifest signature deklarasyonu varsa Authenticode dogrulamasi
- HTTPS remote transport
- fail-closed prerequisite kontrolu
- path traversal engelleme
- atomic staging
- shell-free process invocation
- apply sonrasi state commit
- rollback policy kontrolu
- uninstall basarisi sonrasi state delete
- secret degerlerin manifest icine yazilmamasi

## Artifact Verification

Apply oncesi:

1. size kontrol edilir
2. SHA-256 hesaplanir
3. digest manifest ile karsilastirilir
4. signature deklarasyonu varsa Windows WinVerifyTrust calisir
5. verification basarisizsa stage/apply baslamaz

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
- previous artifact ayni integrity/signature pipeline'indan gecer
- staged package id ve version previous release ile eslesir
- engine basarisizsa mevcut state korunur

## Uninstall

Uninstall installed state uzerinden hedef rootu bulur.

Velopack uninstall non-zero exit ile biterse state korunur.

State yalniz successful package engine sonucundan sonra silinir.

## Secret Boundary

Manifest veya public config private signing key, access token, private feed credential veya production certificate private key tasimaz.

Authentication secret Adapter/credential store sinirinda kalir.
