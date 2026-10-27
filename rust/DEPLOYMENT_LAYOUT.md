<!--
Dosya Yolu: REPO_ROOT/rust/DEPLOYMENT_LAYOUT.md
Amac: Surumlu deployment klasorlerinin ve etkin surum isaretcisinin davranisini tanimlamak
Modul - Markdown Specification
Version: 0.2.0
Aciklama: Hazirlama, etkinlestirme ve geri alma icin mevcut dosya sistemi sozlesmesini aciklar
Bagimli Oldugu Katman: Service | Infrastructure | Documentation
-->

# Versioned Deployment Layout

Status: implementation draft. The activation crate publishes immutable version directories and atomically replaces the active-version record. First-install, forward-update, rollback, and package-declared launch workflows use this crate. Startup recovery and orphan cleanup are not connected yet.

## Layout

For each validated package ID, the configured trusted deployment root contains:

```text
<root>/<package-id>/
  active.json
  integrity/
    <semantic-version>.json
  versions/
    <semantic-version>/
      <application payload>
```

The active record is bounded JSON:

```json
{
  "packageId": "example.product",
  "version": "1.2.3",
  "previousVersion": "1.1.0",
  "entryPoint": "bin/app.exe",
  "previousEntryPoint": "app.exe"
}
```

`previousVersion` and `previousEntryPoint` describe the prior activation. The entry point fields are optional so older active records remain readable. A rollback activation moves the selected entry point into `entryPoint` and records the version being left as the new previous entry.

## Publish and activate

1. A verified package is extracted into a fresh `PackageStage`.
2. `publish_and_activate` copies only regular files and directories to a unique temporary directory below `versions/`.
3. It renames that directory to the immutable version path. Existing versions are not overwritten.
4. It records a bounded JSON inventory of every regular file in `integrity/<semantic-version>.json`, including relative path, byte size, and SHA-256.
5. It writes and syncs a temporary active record, then atomically replaces `active.json` on the same volume.
6. If inventory creation or activation fails, it attempts to remove the unpublished version and its inventory; a process crash may still leave an inactive orphan for future recovery.

Windows uses `MoveFileExW` with replace-existing and write-through flags for the active record. Other platforms use same-directory filesystem rename. Deployment root configuration is trusted input and must be writable by the installer account.

## Recovery boundary

The store resolves `active.json` and verifies that the referenced version directory exists. `verify_current` hashes every regular file and checks the full file set against the saved inventory. This detects local drift when the inventory is unchanged; the inventory is stored beside the deployment and is not itself a cryptographic trust root. The store does not execute files, mutate installed state, write operation checkpoints, clean crash orphans, or automatically roll back. The caller must hold the package-scoped operation lock and coordinate journal/state transitions around activation.

The install workflow uses this crate for initial install and forward update. This contract is not a claim of complete runtime parity or release readiness.
