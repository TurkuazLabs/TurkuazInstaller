<!--
Dosya Yolu: REPO_ROOT/rust/CLI.md
Amac: Community Rust install/update CLI komutlarını ve kaynak gereksinimlerini belgelendirmek
Modul - Markdown
Version: 0.1.0
Aciklama: HTTPS manifest/signature girdilerinden kurulum workflow'una giden ilk kullanım yüzeyini açıklar
Bagimli Oldugu Katman: Application | Documentation
-->

# Community Installer CLI

Status: development CLI. This command line entry point is not the distributed Turkuaz setup and has not passed its current Rust CI compile yet.

## Build and invocation

From the repository root, build or invoke the binary with:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- --help
```

The first install command is:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- install \
  --package-id example.product \
  --manifest-url https://updates.example.test/channel.yaml \
  --signature-url https://updates.example.test/channel.yaml.p7s \
  --trust-store ./manifest-trust.yaml \
  --target ./installed-app \
  --data-root ./installer-data
```

Use `update` instead of `install` to require a previously installed package and a strictly higher SemVer version. Manifest and detached signature URLs must use HTTPS. The artifact URL in the signed release may use HTTPS or a local `file:` URI according to the artifact transfer contract.

## Trust and storage

The trust-store file is supplied out of band and must contain a signer policy for the requested package. The CLI does not create or trust a signer policy automatically. The manifest signature is verified before YAML parsing; the selected artifact is downloaded and checked against the signed exact size and SHA-256, plus Authenticode when required by its descriptor.

The requested target directory contains a `.turkuaz-installer` deployment store with immutable package version directories and an active record. The separate data root contains operation locks, journal, installed state, downloads, and temporary package stages. The CLI prints the active package directory after a successful operation.

## Current limits

This development command does not install prerequisites, create application shortcuts, register Windows integrations, offer progress/cancellation, recover interrupted operations at startup, or remove old deployment versions. It accepts only first install and forward update. It is not a GUI, self-extracting setup, signed release artifact, or production distribution.
