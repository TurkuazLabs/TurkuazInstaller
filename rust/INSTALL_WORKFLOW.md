<!--
Dosya Yolu: REPO_ROOT/rust/INSTALL_WORKFLOW.md
Amac: Verified full package install ve forward update orkestrasyon sözleşmesini açıklamak
Modul - Markdown Specification
Version: 0.1.3
Aciklama: Kilit, stage, etkin deployment ve kurulu state adımlarının sırasını kaydeder
Bagimli Oldugu Katman: Service | Documentation
-->

# Install Workflow

Status: implementation draft. The Rust service wires verified full packages into first-install, forward-update, and policy-gated rollback operations. The development CLI exposes install, update, and policy-gated rollback. This is not a production installer.

## Inputs

The caller supplies a `VerifiedArtifact`, a typed `PackageRelease`, operation type, requested target path, and package extraction limits. The descriptor must equal the signed release artifact descriptor. The artifact token is produced by the existing artifact-validation layer after exact size, digest, and applicable Authenticode checks.

## Operation sequence

1. Acquire the package-scoped OS file lock.
2. Read installed state and current deployment. For install both must be absent. For update both must exist and identify the same version.
3. For update, require the target path to remain the same and the new SemVer precedence to be higher.
4. Write the operation journal checkpoint, then extract the verified archive into a unique staging directory.
5. Copy the staged files to a new immutable version directory and atomically switch the active record.
6. Persist installed state.
7. If state persistence fails, restore the former active version; for a first install remove the active record. Return a distinct compensation failure if the restore also fails.
8. Remove the temporary stage on both success and failure, then complete the operation context.

Journal persistence follows the current best-effort operation-context contract. The CLI has an explicit recovery command for provable state transitions after a crash, but recovery is not automatic at startup. A missing journal or ambiguous first-install state fails closed and may require manual recovery.

## Rollback

The service rollback method requires the caller to provide the signed current release policy. It selects the previous version recorded in active.json, updates installed state, and restores the former active version if state persistence fails. The CLI exposes this operation when given the signed manifest for the currently installed version. It can move one recorded activation at a time; release history and automatic startup recovery are not implemented.

## Current limits

- Install and forward Update are supported by the development CLI; the service also supports policy-gated rollback.
- The package must be a full `.tzpkg`; delta application is not wired. Releases declaring prerequisites, Windows integration actions, or preserved paths are rejected until those adapters are implemented.
- The deployment directory is selected by caller configuration. The workflow records the requested target path in installed state, but does not create shortcuts or a stable launch entry.
- Prerequisite checks/installers, Windows registry/shortcut integration, progress events, cancellation, repair, uninstall, startup recovery, and UI integration remain future work.
- The Windows CLI CI artifact is a development binary, not the Turkuaz setup.
- No Rust tests were run for this implementation. Current branch CI compilation/format status must be checked before treating the implementation as buildable.
