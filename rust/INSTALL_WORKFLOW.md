<!--
Dosya Yolu: REPO_ROOT/rust/INSTALL_WORKFLOW.md
Amac: Verified full package install ve forward update orkestrasyon sözleşmesini açıklamak
Modul - Markdown Specification
Version: 0.1.0
Aciklama: Kilit, stage, etkin deployment ve kurulu state adımlarının sırasını kaydeder
Bagimli Oldugu Katman: Service | Documentation
-->

# Install Workflow

Status: implementation draft. The Rust service currently wires a verified full package into first-install, forward-update, and policy-gated rollback operations. Release discovery/download, prerequisite installation, Windows integration, repair, rollback, uninstall, startup recovery, CLI, and UI are not connected.

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
8. Remove the temporary stage and complete the operation context.

Journal persistence follows the current best-effort operation-context contract. The workflow does not yet reconcile crash states at startup, so it is not a complete transaction/recovery implementation.

## Rollback

The rollback method requires the caller to provide the signed current release policy. It selects the previous version recorded in active.json, updates installed state, and restores the former active version if state persistence fails. It can move one recorded activation at a time; release history and startup recovery are not implemented.

## Current limits

- Install and forward Update are supported by the CLI; the service also supports policy-gated rollback.
- The package must be a full `.tzpkg`; delta application is not wired. Releases declaring prerequisites, Windows integration actions, or preserved paths are rejected until those adapters are implemented.
- The deployment directory is selected by caller configuration. The workflow records the requested target path in installed state, but does not create shortcuts or a stable launch entry.
- Prerequisite checks/installers, Windows registry/shortcut integration, progress events, cancellation, repair, uninstall, and UI integration remain future work.
- No Rust tests were run for this implementation. CI format and workspace compilation are pending on the branch.
