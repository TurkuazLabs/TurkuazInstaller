<!--
Dosya Yolu: REPO_ROOT/rust/PACKAGE_FORMAT.md
Amac: Ozel Turkuaz .tzpkg paket kapsayicisinin surumlu sozlesmesini tanimlamak
Modul - Markdown Specification
Version: 0.2.0
Aciklama: Paket archive v1/v2 metadata, entry point ve payload path kurallarini belgeler
Bagimli Oldugu Katman: Service | Tool | Documentation
-->

# Turkuaz Package Format

Status: v1/v2 implementation draft. Build, staging, activation, and package-declared launch entry points are implemented in the development workflow.

## Archive structure

A `.tzpkg` file is a ZIP archive with exactly two top-level entries:

- `turkuaz-package.json`: UTF-8 JSON metadata, at most 64 KiB.
- `payload/`: application files and directories.

Version 1 metadata remains supported and contains only the package ID and version:

```json
{
  "formatVersion": 1,
  "packageId": "example.product",
  "version": "1.2.3"
}
```

Version 2 adds a required `entryPoint` relative path:

```json
{
  "formatVersion": 2,
  "packageId": "example.product",
  "version": "1.2.3",
  "entryPoint": "bin/app.exe"
}
```

Both versions reject unknown fields and require package ID/version to match the authenticated release. Version 1 has no declared entry point; version 2 requires one. The entry point must use forward slashes and identify a regular file beneath `payload/`. Payload entries must be descendants of `payload/`; files at the archive root are invalid.

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

Only regular files and directories are accepted. Payload permissions and ownership are not part of either format version; the current installer target is Windows.

## Build and staging API

- `build_full_package(source_directory, package_id, version, output_file, limits)` creates a v1-compatible `.tzpkg` without an entry point or overwriting an existing output.
- `build_full_package_with_entry_point(..., entry_point, limits)` creates format v2 when an entry point is supplied; the builder verifies it names a regular source file.
- `stage_full_package(verified_artifact, release, payload_directory, limits)` validates the metadata and entry point, then returns a `PackageStage` carrying the optional path.
- The default extraction limits are 250,000 archive entries, 4 GiB per file, and 32 GiB total expanded payload.

The install workflow publishes immutable versions and stores the active and previous entry points in the active record. `launch` uses the active package entry point by default. Existing v1 packages can still be launched by passing `--executable` explicitly. Repair, uninstall, startup recovery, and production setup packaging remain later migration work.
