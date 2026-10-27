<!--
Dosya Yolu: REPO_ROOT/rust/CLI.md
Amac: Community Rust install/update CLI komutlarını ve kaynak gereksinimlerini belgelendirmek
Modul - Markdown
Version: 0.3.1
Aciklama: HTTPS manifest/signature girdilerinden kurulum workflow'una giden ilk kullanim yuzeyini aciklar
Bagimli Oldugu Katman: Application | Documentation
-->

# Community Installer CLI

Status: development CLI. This command line entry point is not the distributed Turkuaz setup.

## Build and invocation

Relative target and data-root paths are resolved from the current working directory. Package-state checks compare the resolved target path with the path recorded during installation.

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

Use `update` instead of `install` to require a previously installed package and a strictly higher SemVer version. `repair` re-fetches the signed package for the currently installed version, verifies the artifact, builds a new immutable deployment, and atomically switches the active record. It does not modify the existing deployment in place. Manifest and detached signature URLs must use HTTPS. The artifact URL in the signed release may use HTTPS or a local `file:` URI according to the artifact transfer contract.

Repair a package by passing the signed manifest and detached signature for its installed version:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- repair \
  --package-id example.product \
  --manifest-url https://updates.example.test/channel.yaml \
  --signature-url https://updates.example.test/channel.yaml.p7s \
  --trust-store ./manifest-trust.yaml \
  --target ./installed-app \
  --data-root ./installer-data
```

The package ID, installed version, and target path must match local state. If a repair is interrupted, run `recover` before retrying.

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

## List installed packages

Show the installed package ID, version, release channel, and target path recorded in the configured data root:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- list \
  --data-root ./installer-data
```

This command holds each package's operation lock and checks that its active deployment version matches installed state. It prints one tab-separated row per package and reports an empty list when none are installed; a missing or mismatched active record is reported as an error.

## Uninstall a package

Uninstall requires the exact target path recorded at install time and the explicit confirmation value `YES`. It removes only this package's managed version directory and installed-state record; unrelated files under the target directory and data root are preserved. The package operation lock is held through the operation.

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- uninstall \
  --package-id example.product \
  --target ./installed-app \
  --data-root ./installer-data \
  --confirm-uninstall YES
```

If an operation journal already exists, run `recover` first. If uninstall is interrupted after its managed directory is moved to quarantine, run `recover` with the same package ID and paths to finish the state and filesystem cleanup.

## Recover an interrupted operation

Run this after a process or machine interruption if later commands report a mismatch between installed state and the active deployment:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- recover \
  --package-id example.product \
  --target ./installed-app \
  --data-root ./installer-data
```

Recovery takes the package lock. It verifies and clears a stale journal when state and active version already agree. For an interrupted uninstall, it either clears a journal if the package is still active, or completes removal of the quarantined managed package when state indicates uninstall had begun. If a journal proves that an update or rollback activated its target but did not save installed state, it verifies the active file inventory and completes the state write. It clears a journal for an install that never activated a deployment. Ambiguous state, a missing journal with mismatched state, or a first install that activated but lacks installed state stops for manual recovery. Abandoned staging data is left untouched. This is an explicit CLI command; it does not run automatically at startup.

## Launch the installed application

The CLI uses the entry point declared by a v2 package automatically. For v1 packages without one, pass `--executable` explicitly. An optional `--executable` can also override the package default. It checks package state and the active version under the package lock, verifies the active deployment inventory, and starts only a regular file that resolves inside that deployment. Arguments after `--` are passed directly to the process without a shell:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- launch \
  --package-id example.product \
  --target ./installed-app \
  --data-root ./installer-data \
  -- --profile test
```

Use forward slashes in the relative executable path. The command prints the started process ID. GUI and start-menu integration are not implemented yet.

## Verify installed files

After installation or update, compare the active deployment's files with the inventory recorded at publication:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- verify \
  --package-id example.product \
  --target ./installed-app \
  --data-root ./installer-data
```

The command holds the package operation lock, checks installed state against the active version, and hashes every regular file. It detects additions, removals, and size/content changes relative to the local inventory. The inventory is stored alongside the versioned deployment and is not a cryptographic trust root; this check does not replace signed manifest or artifact verification.

## Build a package

The CLI exposes the custom package builder:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- build-package \
  --package-id example.product \
  --version 1.2.3 \
  --source ./publish-directory \
  --output ./artifacts/example-product-1.2.3.tzpkg \
  --executable bin/app.exe
```

The builder rejects unsafe paths and overwriting an existing output. Pass `--executable bin/app.exe` to embed an entry point and create format v2; omitting it creates a backward-compatible v1 package. Generate an unsigned manifest draft from the finished archive's exact size and SHA-256, then sign it separately.

## Generate an unsigned release manifest draft

`describe-package` streams the finished `.tzpkg` to calculate its exact size and SHA-256, then writes a schema v1 manifest draft after validating it with the installer manifest parser. It refuses to overwrite an existing output. The draft is not signed; sign the exact generated bytes with the release signer before publishing it.

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- describe-package \
  --package-id example.product \
  --version 1.2.3 \
  --channel stable \
  --package ./artifacts/example-product-1.2.3.tzpkg \
  --uri https://updates.example.test/example-product-1.2.3.tzpkg \
  --target ./installed-app \
  --output ./artifacts/channel.yaml
```

The output uses a conservative policy (`rollback.supported: false`) and contains no private key material. Review the URI and install target before signing. For a package requiring rollback, edit the policy before signing. The detached CMS signing steps and out-of-band trust setup are documented in [RELEASE_SIGNING.md](RELEASE_SIGNING.md).

## Trust and storage

The trust-store file is supplied out of band and must contain a signer policy for the requested package. A non-production shape example is [manifest-trust.example.yaml](examples/manifest-trust.example.yaml); replace the example package, publisher subject, and zero certificate pin with the actual values before use. The CLI does not create or trust a signer policy automatically. The manifest signature is verified before YAML parsing; the selected artifact is downloaded and checked against the signed exact size and SHA-256, plus Authenticode when required by its descriptor.

The requested target directory contains a `.turkuaz-installer` deployment store with immutable package version directories and an active record. The separate data root contains operation locks, journal, installed state, downloads, and temporary package stages. The CLI prints the active package directory after a successful operation.

The Rust Migration CI workflow builds `turkuaz-installer-cli-development-windows-x64` after the Windows compile passes. GitHub Actions retains that development binary for seven days. It is not the Turkuaz setup and is not a production release.

## Current limits

This development command rejects releases that require prerequisites, shortcuts, protocol registration, or preserved paths because those adapters are not implemented yet. It does not offer progress/cancellation, recover interrupted operations at startup, or automatically remove unreferenced deployment directories. It accepts first install, forward update, same-version repair, and policy-gated rollback. Rollback requires the signed current release policy and selects only the previous recorded activation.

The current binary is headless. It is not a GUI, self-extracting setup, signed release artifact, or production distribution. No Rust tests were run; consult the latest Rust Migration CI run for current format and compilation status.

## Offline setup bundles

Create a portable `.tzsetup` from an already signed release. The trust store is supplied locally and is never copied into the bundle:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- build-setup-bundle \
  --package-id example.product \
  --manifest ./release.yaml \
  --signature ./release.p7s \
  --trust-store ./manifest-trust.yaml \
  --package ./application.tzpkg \
  --output ./TurkuazSetup.tzsetup
```

The command verifies the detached manifest signature against the package-specific trust policy, verifies the package's signed size and SHA-256 (and Authenticode when declared), then writes the exact verified inputs. It does not sign releases or produce a self-extracting Windows executable.

Install from removable media or a local path without network access:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- install-bundle \
  --package-id example.product \
  --bundle ./TurkuazSetup.tzsetup \
  --trust-store ./manifest-trust.yaml \
  --target ./installed-app \
  --data-root ./installer-data \
  --operation install
```

Use `--operation update` for a higher signed version, `repair` for the currently installed version, or `rollback` to restore the previous recorded deployment under the signed rollback policy. The installer rechecks the external trust policy, package digest, optional Authenticode signature, and package archive before activation. The bundle format accepts only its fixed, bounded entries and extracts them into temporary staging under the data root. A graphical bootstrapper, paired NSIS backup, and automated production signing/publishing are still pending.

## Self-extracting Windows setup

Package a verified offline bundle behind a Windows CLI executable stub:

```sh
cargo run --manifest-path rust/Cargo.toml -p turkuaz-installer-cli -- build-self-extracting-setup \
  --stub ./rust/target/release/turkuaz-installer.exe \
  --bundle ./TurkuazSetup.tzsetup \
  --output ./TurkuazSetup.exe
```

The `.exe` stores the bundle after the stub with a fixed footer, bounded length, and SHA-256. The stub can extract its own payload and invoke the same signed bundle workflow:

```sh
TurkuazSetup.exe install-self \
  --package-id example.product \
  --trust-store ./manifest-trust.yaml \
  --target ./installed-app \
  --data-root ./installer-data \
  --operation install
```

The signer trust policy remains an explicit external input. This first Windows setup is command-line driven and must be Authenticode-signed after the final payload is attached for production distribution. The GUI, automated signing/publishing, and paired NSIS backup are still pending.
