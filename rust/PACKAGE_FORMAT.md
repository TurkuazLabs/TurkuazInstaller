<!--
Dosya Yolu: REPO_ROOT/rust/PACKAGE_FORMAT.md
Amac: Ozel Turkuaz .tzpkg paket kapsayicisinin surumlu sozlesmesini tanimlamak
Modul - Markdown Specification
Version: 0.1.0
Aciklama: Paket uretimi ve staging icin ortak v1 archive, metadata ve path kurallarini belgeler
Bagimli Oldugu Katman: Service | Tool | Documentation
-->

# Turkuaz Package Format

Status: v1 implementation draft. Package build and staging APIs are present; install-target activation, release signing workflow, and runtime integration are not complete.

## Archive structure

A `.tzpkg` file is a ZIP archive with exactly two top-level entries:

- `turkuaz-package.json`: UTF-8 JSON metadata, at most 64 KiB.
- `payload/`: application files and directories.

Example metadata:

```json
{
  "formatVersion": 1,
  "packageId": "example.product",
  "version": "1.2.3"
}
```

Version 1 rejects unknown metadata fields and requires the package ID and version to match the typed release model from the authenticated manifest. Payload entries must be descendants of `payload/`; files at the archive root are invalid.

The installer extracts the contents below `payload/` into a fresh staging directory. It does not copy `turkuaz-package.json` into the installed application.

## Trust boundary

The signed release manifest is authoritative for package identity, version, exact byte size, SHA-256 digest, and any declared Authenticode signer policy. The archive metadata repeats package identity/version so the engine can reject a package that does not match that release. It does not replace or weaken manifest signature and artifact digest validation.

The package builder runs before the final artifact descriptor is created: it receives the typed package ID/version, writes the `.tzpkg`, then the release process measures and hashes that output before creating the signed release manifest.

## Payload path rules

The package engine rejects:

- Absolute, rooted, drive-qualified, UNC, traversal, NUL, and backslash paths.
- Windows-invalid characters, trailing spaces/dots, and reserved device names.
- Duplicate entries and names that collide when compared case-insensitively.
- Symlinks and special files.
- Entries whose declared or streamed size exceeds configured per-file/total limits.

Only regular files and directories are accepted. Payload permissions and ownership are not part of the v1 contract; the current installer target is Windows.

## Build and staging API

- `build_full_package(source_directory, package_id, version, output_file, limits)` creates a `.tzpkg` without overwriting an existing output.
- `stage_full_package(verified_artifact, release, payload_directory, limits)` checks the artifact descriptor and internal metadata, then extracts only a verified package into a new stage directory.
- The default extraction limits are 250,000 archive entries, 4 GiB per file, and 32 GiB total expanded payload.

The current contract stops at staging. Version activation, install/update/repair/rollback/uninstall orchestration, package signing/release generation, and startup recovery are tracked as later migration work.
