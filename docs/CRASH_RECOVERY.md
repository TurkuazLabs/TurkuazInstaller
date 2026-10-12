# 📄 Dosya Yolu: /docs/CRASH_RECOVERY.md
# 📌 Amac: Yarim kalan installer update, repair, rollback ve uninstall operasyonlarinda fail-closed guvenlik kapisi
# 📌 Modul - Dokumantasyon
# Version: 1.7.1
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

The Application layer also checks the requested `resumeEntry` against
the **exact persisted journal record under the package lock**, including
operation ID and phase. A missing journal or fabricated reboot-safe record
must never bypass an orphaned `Applying` checkpoint. CLI request
validation remains independently required; this is defense in depth.

Records explicitly interrupted in pre-apply `Verifying` or `Staging`
can be retried, subject to the normal signed-manifest checks and the
package-scoped lock. A stale `Completed` record is not considered evidence
of a partial mutation.

A pre-apply record is **not** safe to retry when the journal package ID,
committed state package ID, or installed target disagrees. Under the
package lock, the live mutation workflow now reads committed state and
uses the same metadata conflict predicate as the read-only assessment;
conflicts retain the old journal and block download/stage/apply.
Update/repair/rollback/uninstall journals without committed state are
also held. A completed uninstall journal may legitimately have no
committed state, and must not create an artificial recovery hold.

## Read-only recovery assessment

`InstallerRecoveryAssessmentService` provides a typed, **read-only**
diagnostic snapshot for the installer UI / future recovery tooling. It reads
the persisted operation journal and committed installed-state repository,
using the **same mutation/reboot checkpoint policy as the live workflow**.

The report exposes the operation ID, requested and committed version,
journal phase, interrupted phase and recorded target paths. It distinguishes
`NoJournal`, `NoMutationHold`, `PendingRebootResume`,
`RecoveryRequired`, and `MetadataConflict` (including a persisted
update journal with missing committed state or mismatched install target).

This is **not** an authorization to repair, retry or remove any package:
- `NoMutationHold` only says the journal has no known unsafe mutation
  checkpoint; signed-manifest and runtime policy validation are still required.
- A report does **not** scan actual installed binaries, verify their
  Authenticode publisher, prove folder ownership, or trust journal version
  fields as on-disk evidence.
- Reads are observational, not an atomic snapshot with the updater; callers
  must never clear a lock based on the result.
- Storage read failures propagate instead of being treated as `NoJournal`.
  The assessment deliberately never invokes `SaveAsync` or `DeleteAsync`.

Infrastructure regression tests compare the raw persisted state/journal
JSON bytes before and after inspecting the disk-V2/state-V1 scenario. Both
real Windows process-kill scenarios also assert that the read-only report
detects their orphaned `Applying` operation without unlocking it.

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

Two independently isolated windows-hosted scenarios now exercise the
same recovery gate. The original scenario terminates the child **before**
`Update.exe` starts and requires V1 on disk and V1 in the committed JSON.
The second scenario invokes the **real Velopack `Update.exe`**, waits for
successful exit and verifies the V2 version marker, then parks the workflow
**before committing the new installed-state JSON** and terminates the real
child process. Recovery must observe **disk V2 / committed JSON V1**,
a durable orphaned `Applying` record, unchanged user data, and blocked
update/uninstall requests. The two independent fixtures are removed only
on the disposable GitHub-hosted Windows worker.

Verified in [hosted Windows Velopack CI #38303860363](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/38303860363):
- `TURKUAZ_INSTALLER_REAL_PROCESS_KILL_APPLY_CHECKPOINT_HOLD_OK`
- `TURKUAZ_INSTALLER_REAL_POST_UPDATER_KILL_DISK_STATE_DIVERGENCE_HOLD_OK`

The second acceptance covers a **real V2 disk / committed V1 mismatch**,
the original durable `Applying` operation ID, preserved user data and
an explicit fail-closed hold on update and uninstall after process restart.
Both synthetic fixture installations were run in separate disposable
test directories, never against a user application.

**Precise limitation:** this does not kill `Update.exe` *while* it writes
files. The process terminated after the actual updater returned, but before
the workflow committed new state. A real updater killed in the middle of
a file replacement, signed artifact recovery, and trusted automatic
reconciliation/rollback are still outstanding acceptance gates. Neither
case authorizes production machine-wide recovery.

## Running-updater process termination LAB gate

The hosted-Windows E2E now includes a third **isolated, disposable**
scenario. After the real workflow persists `Applying`, the fixture
runner starts the real install-root `Update.exe`. A signal publishes
the updater PID only after process creation. The parent requires that
this PID still identifies a live `Update.exe`, then kills the
workflow process tree and waits for that updater process to exit.

A successful check establishes that:
- the real updater process was running when the parent observed it,
  and was terminated with the fixture workflow process tree;
- committed JSON remains V1 and the orphaned `Applying` journal
  retains the same operation ID;
- the read-only assessment requires human review;
- new update/uninstall operations are refused without journal rewrite;
- external fixture UserData bytes survive unchanged.

The current on-disk application marker is **not** required to equal V1
or V2 after this test. It may be missing or mixed after a process
termination. Neither a V1 nor V2 marker can authorize reconciliation.

This does **not** prove that termination fell inside a specific file
write or filesystem replacement. It also does not prove completeness
of a partial payload. A reproducible mid-file fault injection, full
on-disk inventory, signed recovery package verification, and explicit
privileged repair remain separate acceptance gates. The test stays
GitHub-hosted Windows LAB only; it must not target customer installs.
