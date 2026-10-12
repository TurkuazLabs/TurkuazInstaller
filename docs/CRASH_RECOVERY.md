# 📄 Dosya Yolu: /docs/CRASH_RECOVERY.md
# 📌 Amac: Yarim kalan installer update, repair, rollback ve uninstall operasyonlarinda fail-closed guvenlik kapisi
# 📌 Modul - Dokumantasyon
# Version: 1.2.0
# Aciklama: Durable checkpoint, interruption phase, manual reconcile ve acik production blokajlari
# Bagimli Oldugu Katman: Service | Repo | Tool

# Crash During Apply — Recovery Safety Hold

The package operation journal already records a package-scoped operation ID,
target, desired version and phase. The new `InterruptedPhase` field records
which checkpoint was active **before** an operation failed or was cancelled.
Old JSON documents without this optional field remain readable.

## Before package mutations

When a configured journal repository exists, checkpoint writes for
`Applying`, `ApplyingIntegrations`, `SavingState` and `RemovingState`
are now **required**. If a durable journal write fails, the workflow must
not invoke the corresponding mutation. Structured diagnostic logging
outside these critical checkpoints remains best-effort.

After acquiring the package-scoped operation lock, a new manual operation
checks the previous journal before writing a new operation ID. The workflow
throws a typed `InstallerRecoveryRequiredException` without downloading,
staging, applying or committing another package when the previous checkpoint
was one of those mutation phases, or a `Failed`/`Cancelled` record says that
it was interrupted in such a phase.

Legacy `Failed`/`Cancelled` journal records whose interrupted phase cannot
be determined are also held. An `AwaitingReboot` or `RebootResumeArmed`
journal blocks a *new manual* operation; only the existing authenticated
reboot-resume path can continue those checkpoints.

Records explicitly interrupted in pre-apply `Verifying` or `Staging`
can be retried, subject to the normal signed-manifest checks and the
package-scoped lock. A stale `Completed` record is not considered evidence
of a partial mutation.

## Important limits

**A green recovery-hold test is not automatic crash rollback.** The installed
files might already have changed while committed state still names the prior
version. This patch intentionally makes no attempt to decide which installed
files to delete or overwrite based only on the journal.

Before clearing a recovery hold, a future explicit recovery workflow must:
1. Verify the installed version and payload files independently of the
   persisted state; never use a journal version as trusted file evidence.
2. Resolve the correct product's release through the trusted signed-manifest
   provider and validate artifact size, SHA-256 and publisher/signature policy.
3. Coordinate the package-scoped operation lock and the application's
   privileged install scope, including Windows integration receipts.
4. Select a trusted repair or rollback artifact, preserve user data, verify
   the final on-disk version, and *then* reconcile committed state and journal.
5. Refuse the operation and retain the hold when any evidence is missing,
   ownership is ambiguous, or validation fails.

There is deliberately **no automatic journal deletion / blind retry button**.
The Program Files selector stays disabled and PR #54 remains draft.
Product signing, privileged multi-user maintenance, UAC accept/deny, and a
real process-termination-during-update E2E remain separate acceptance gates.

Unit tests cover simulated apply failure followed by a new service instance,
orphaned mutation phases, legacy journal records, safe pre-apply retry,
failed durable apply checkpoint and JSON phase round-tripping.

A separate **persisted-disk integration regression**
(`InstallerPersistedRecoverySafetyTests` in Infrastructure tests) uses
real filesystem application markers, a separate user-data file, an actual
atomic JSON installed-state repository and an actual JSON operation journal.
The controlled LAB package adapter modifies the V2 marker *before* failing.
A fresh service and freshly opened repositories must then reject update
and uninstall, leave the committed V1 state untouched, preserve the
interrupted operation ID and keep user data unchanged. Another test
loads an orphaned `Applying` checkpoint directly from persisted JSON.
This detects the dangerous disk-V2/state-V1 divergence across process
service instances, without inventing an automatic fix.

These persisted-disk tests simulate interruption after file mutation;
they **do not** terminate a real Velopack or Windows Installer process.

A separate hosted-Windows Velopack E2E gate now starts a second .NET
workflow process against a genuine V1 Velopack fixture and real atomic
JSON state/journal stores. After signed-size/digest-checked artifact
download and staging, the child reaches the durable `Applying` checkpoint.
A **test-only** process adapter signals immediately before launching
`Update.exe` and suspends; the parent kills the actual child OS process.
After restart, the orphaned `Applying` journal must still exist, state,
installed V1 files and user data must remain intact, and both new update
and uninstall requests must be rejected with
`InstallerRecoveryRequiredException`. The parent cleans up only its
disposable LAB fixture using the real Velopack uninstall adapter.

The test only runs when `GITHUB_ACTIONS=true` and
`RUNNER_ENVIRONMENT=github-hosted`. Local runs explicitly mark this
extra test skipped; there is no system-wide install or antivirus bypass.
If the hosted check succeeds, its log contains
`TURKUAZ_INSTALLER_REAL_PROCESS_KILL_APPLY_CHECKPOINT_HOLD_OK`.

**Precise limitation:** the killed process is the .NET workflow process
after the journal checkpoint, **before** `Update.exe` performs disk
mutation. The earlier simulated file-mutation regression covers the
disk-V2/state-V1 divergence. Killing a real updater *during* active
file replacement and proving trusted reconciliation/automatic rollback
are still outstanding, higher-risk acceptance gates. Neither type of
test authorizes production machine-wide recovery.
