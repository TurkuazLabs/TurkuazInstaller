<!--
Dosya Yolu: REPO_ROOT/rust/INSTALL_WORKFLOW.md
Amac: Verified full package install ve forward update orkestrasyon sözleşmesini açıklamak
Modul - Markdown Specification
Version: 0.2.4
Aciklama: Kilit, stage, etkin deployment ve kurulu state adımlarının sırasını kaydeder
Bagimli Oldugu Katman: Service | Documentation
-->

# Install Workflow

Status: implementation draft. The Rust service wires verified full packages into first-install, forward-update, and policy-gated rollback operations. The development CLI exposes install, update, same-version repair, and policy-gated rollback. This is not a production installer.

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

Journal persistence follows the current best-effort operation-context contract. The CLI retains an explicit recovery command for manual use. Online install/update/repair/rollback and verified offline bundle operations check for a pending journal after verifying the incoming signed release and artifact when applicable, then run conservative recovery before starting the requested operation. The CLI also checks and recovers before `launch`, `verify`, `uninstall`, and each `list` entry backed by installed state with a known target path. On a no-argument launch, CLI and GUI startup scan the default data root's canonical journal files and attempt conservative recovery using each recorded target. A malformed or ambiguous record blocks the GUI or stops CLI startup. A custom data root is checked by the per-package preflight when an operation is selected. A missing journal with mismatched state or an ambiguous first-install state fails closed and may require manual recovery.

## Repair

The repair method requires installed state and active deployment to agree on package, version, and target path. The caller supplies the signed release and verified artifact for that exact installed version. The workflow stages a fresh package copy in a unique immutable deployment directory, writes its inventory, and atomically updates the active record. The previous rollback hint is preserved. A crash before pointer replacement leaves the old deployment active; after replacement, the complete repaired deployment is active. An interrupted repair journal can be cleared by explicit recovery and the repair safely retried.

## Rollback

The service rollback method requires the caller to provide the signed current release policy. It selects the previous version recorded in active.json, updates installed state, and restores the former active version if state persistence fails. The CLI exposes this operation when given the signed manifest for the currently installed version. It can move one recorded activation at a time; release history is not implemented. A no-argument launch scans the default journal root and attempts conservative recovery for each record; custom data roots still rely on per-package preflight.

## Current limits

- Install, forward Update, same-version Repair, and policy-gated rollback are supported by the development CLI and service.
- The package must be a full `.tzpkg`; delta application is not wired. Releases declaring prerequisites, Windows integration actions, or preserved paths are rejected until those adapters are implemented.
- The deployment directory is selected by caller configuration. The workflow records the requested target path in installed state, but does not create shortcuts or a stable launch entry.
- Prerequisite checks/installers and Windows registry/shortcut integration remain future work. On no-argument launch, the GUI shows startup recovery results and blocks new setup if recovery is ambiguous; custom data roots are checked per package operation.
- The Windows CLI CI artifact is a development binary, not the Turkuaz setup.
- CI also creates an ephemeral Windows test certificate, verifies a detached CMS-signed offline bundle, and runs install, update, launch, tamper detection, repair, rollback, launch, and uninstall. Acceptance with the production publisher certificate and on a clean end-user VM remains unrun.
