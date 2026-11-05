<!--
Dosya Yolu: REPO_ROOT/rust/RELEASE_SIGNING.md
Amac: Community Rust release manifest taslagini guvenli sekilde detached CMS imzasi ile yayina hazirlamak
Modul - Markdown
Version: 0.1.0
Aciklama: Private key'i repodan ve istemciden ayirarak imzali manifest ve dis trust policy olusturma akisidir
Bagimli Oldugu Katman: Release Engineering | Documentation
-->

# Community release manifest signing

This guide signs the exact bytes produced by `describe-package`. The Rust CLI verifies a detached CMS/PKCS#7 signature before parsing YAML. Its signer must match an out-of-band package trust policy by certificate subject and SHA-256 pin.

## 1. Build and describe the package

On a release workstation, build the package and draft the manifest:

```powershell
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- build-package `
  --package-id example.product `
  --version 1.2.3 `
  --source .\publish-directory `
  --output .\artifacts\example-product-1.2.3.tzpkg

cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- describe-package `
  --package-id example.product `
  --version 1.2.3 `
  --channel stable `
  --package .\artifacts\example-product-1.2.3.tzpkg `
  --uri https://updates.example.test/example-product-1.2.3.tzpkg `
  --target .\installed-app `
  --output .\artifacts\channel.yaml
```

Review the package ID, version, channel, artifact URL, install target, and rollback policy. The package must already be uploaded at the URL before clients install it. If the release needs rollback, set `rollback.supported: true` before signing. Do not edit the manifest after signing.

## 2. Sign exact manifest bytes

Use the release signing certificate and private key from an approved protected signing environment (prefer a hardware-backed or managed key). Do not put private keys, passwords, or signing credentials in the repository, the package, the client trust store, or a CI log.

OpenSSL can create a detached DER CMS signature:

```sh
openssl cms -sign -binary \
  -in artifacts/channel.yaml \
  -signer release-signer.pem \
  -inkey release-private-key.pem \
  -outform DER \
  -out artifacts/channel.yaml.p7s \
  -md sha256 \
  -nosmimecap
```

The resulting `.p7s` is detached: the manifest bytes remain in `channel.yaml`. Keep those exact bytes through upload. Publish the manifest and signature together at the URLs supplied to the installer. Protect the signing key with the signing environment's normal access controls and backup policy.

Before publishing, verify that the signature matches the final manifest:

```sh
openssl cms -verify -binary \
  -inform DER \
  -in artifacts/channel.yaml.p7s \
  -content artifacts/channel.yaml \
  -noverify \
  -out /dev/null
```

`-noverify` skips OpenSSL's certificate-chain validation for this local signature consistency check; it does not skip the CMS content signature check. The installer uses its package-scoped signer subject and certificate pin policy instead. This command does not establish that a signer is trusted.

## 3. Configure client trust out of band

Provision the trust store independently of the release manifest. For each package, set the publisher subject to the exact subject representation returned by Windows CryptoAPI's `CertNameToStrW` using X.500 format with reverse flag, and set the certificate pin to lowercase SHA-256 of the signer's DER-encoded certificate. The subject and pin must come from the certificate actually used to sign. The zero pin and example subject in [examples/manifest-trust.example.yaml](examples/manifest-trust.example.yaml) are placeholders and must never be shipped as production trust.

Keep the trust store and pin distribution under administrator-controlled configuration. Do not accept a subject or pin supplied by the manifest being verified. This verifier checks the CMS signature, signer certificate validity time, subject, and pin; its current contract does not build a public certificate chain.

## 4. Install from the signed release

After publishing both files and provisioning the trust policy, use their HTTPS URLs with the CLI:

```powershell
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- install `
  --package-id example.product `
  --manifest-url https://updates.example.test/channel.yaml `
  --signature-url https://updates.example.test/channel.yaml.p7s `
  --trust-store .\manifest-trust.yaml `
  --target .\installed-app `
  --data-root .\installer-data
```

The installer checks the signature before YAML parsing and checks the downloaded package's exact byte size and SHA-256 against the signed manifest. The current Rust CLI is a development tool; this workflow does not produce the Turkuaz setup or the paired NSIS backup.
