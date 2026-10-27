<!--
Dosya Yolu: REPO_ROOT/rust/CLI.md
Amac: Community Rust install/update CLI komutlarını ve kaynak gereksinimlerini belgelendirmek
Modul - Markdown
Version: 0.1.2
Aciklama: HTTPS manifest/signature girdilerinden kurulum workflow'una giden ilk kullanim yuzeyini aciklar
Bagimli Oldugu Katman: Application | Documentation
-->

# Community Installer CLI

Status: development CLI. This command line entry point is not the distributed Turkuaz setup. Current branch CI compilation is pending.

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

To roll back, pass the signed manifest and detached signature for the currently installed version. The signed rollback policy must allow rollback, and the active deployment must have a previous version recorded. Rollback does not download an artifact:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- rollback \
  --package-id example.product \
  --manifest-url https://updates.example.test/channel.yaml \
  --signature-url https://updates.example.test/channel.yaml.p7s \
  --trust-store ./manifest-trust.yaml \
  --target ./installed-app \
  --data-root ./installer-data
```

## Build a package

The CLI exposes the custom package builder:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- build-package \
  --package-id example.product \
  --version 1.2.3 \
  --source ./publish-directory \
  --output ./artifacts/example-product-1.2.3.tzpkg
```

The builder rejects unsafe paths and overwriting an existing output. This command creates only the `.tzpkg`; it does not calculate or sign a release manifest. The release descriptor must be generated from the finished archive's exact size and SHA-256, then signed separately.

## Trust and storage

The trust-store file is supplied out of band and must contain a signer policy for the requested package. A non-production shape example is [manifest-trust.example.yaml](examples/manifest-trust.example.yaml); replace the example package, publisher subject, and zero certificate pin with the actual values before use. The CLI does not create or trust a signer policy automatically. The manifest signature is verified before YAML parsing; the selected artifact is downloaded and checked against the signed exact size and SHA-256, plus Authenticode when required by its descriptor.

The requested target directory contains a `.turkuaz-installer` deployment store with immutable package version directories and an active record. The separate data root contains operation locks, journal, installed state, downloads, and temporary package stages. The CLI prints the active package directory after a successful operation.

The Rust Migration CI workflow builds `turkuaz-installer-cli-development-windows-x64` after the Windows compile passes. GitHub Actions retains that development binary for seven days. It is not the Turkuaz setup and is not a production release.

## Current limits

This development command rejects releases that require prerequisites, shortcuts, protocol registration, or preserved paths because those adapters are not implemented yet. It does not offer progress/cancellation, recover interrupted operations at startup, or remove old deployment versions. It accepts first install, forward update, and policy-gated rollback. Rollback requires the signed current release policy and selects only the previous recorded activation. Repair and uninstall are not implemented.

The current binary is headless. It is not a GUI, self-extracting setup, signed release artifact, or production distribution. No Rust tests were run; consult the latest Rust Migration CI run for current format and compilation status.
