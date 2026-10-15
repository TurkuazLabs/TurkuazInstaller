# 📄 Dosya Yolu: /docs/SIGNED_PAYLOAD_INVENTORY.md
# 📌 Amac: Imzali dosya SHA-256 envanteri formati ve recovery icin trust sinirlarini tanimlar
# 📌 Modul - Documentation Markdown
# Version: 1.6.0
# Aciklama: Detached CMS dogrulamasi, tam release binding, portable path kurallari ve salt-okunur kapsam
# Bagimli Oldugu Katman: Tool | Service | Domain

# Signed installed-payload inventory — validation-only foundation

`SignedPayloadInventoryReader` validates one detached CMS/PKCS#7 signature
before parsing any untrusted JSON. It reuses the existing package-specific
`CmsManifestSignatureVerifier`, which checks the signer certificate
subject and independently configured SHA-256 certificate pin.

The caller must supply a `PackageRelease` already obtained via the
existing verified release-manifest workflow. The inventory's exact
`package_id`, `version`, `channel` and `artifact_sha256` must
match that release. A detached signature **alone** is insufficient.

## Envelope and schema

Save these JSON UTF-8 bytes together with a detached signature
(e.g. `payload-inventory.json` and `payload-inventory.json.p7s`).
The verifier authenticates the **exact original bytes**; it must not
canonicalize or reserialize JSON before verifying.

```json
{
  "schema_version": 1,
  "package_id": "turkuazlabs.turkuazinstaller.e2e",
  "version": "2.0.0",
  "channel": "stable",
  "artifact_sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "files": [
    {
      "path": "current/example.dll",
      "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
    }
  ]
}
```

The digest strings above are **illustrative placeholders**, not hashes
of a real release. Both hashes must be generated from the actual verified
artifact and expected installed file bytes during trusted packaging.

The reader rejects missing or extra JSON fields, duplicate JSON property
names, duplicate file paths (case-insensitively), unsupported schema,
invalid typed values, empty lists, more than 4096 entries and input larger
than the shared bounded manifest/signature limits. Paths use forward
slashes only, are relative, and reject traversal, invalid device names,
Windows alternate data streams, and ambiguous segments.

## Security boundary

- A valid signature authenticates *who signed the supplied list*, not
  that the list contains **all** files from the Velopack package.
- A matching `artifact_sha256` binds the inventory to the distribution
  artifact digest from a previously trusted release. It does not independently
  validate that each declared file was extracted from that artifact.
- The current diagnostic `InstallerPayloadEvidenceService` hashes actual
  installed bytes and distinguishes match/mismatch/missing. It must still
  be provided the validated inventory entries by an authorized orchestration
  layer; no product/runtime recovery integration is enabled here.
- No verification here authorizes reconciliation, journal deletion,
  `UpdateAsync`, `RollbackAsync`, uninstall, or unattended file replacement.
- Further production acceptance requires trusted inventory generation
  from package contents; complete path coverage (including expected file
  attributes); trusted install ownership and scope; symlink/TOCTOU hardening;
  safe signed recovery artifacts; and explicit operator acceptance.

Tests use ephemeral LAB certificates and real `SignedCms` signatures,
then compare a signed V1 reference with changed on-disk test bytes without
modifying either the installed file or any installer journal.

## Unsigned inventory builder from a verified full nupkg

`VelopackPayloadInventoryBuilder.BuildUnsignedAsync` now takes an existing
**verified full** `-full.nupkg` file and an independently trusted
`PackageRelease`. It first checks the package length and SHA-256 against
that release and then streams the ZIP payload beneath `lib/app/`.
Each archive file maps to `current/<relative-file>` and contributes
a SHA-256 digest computed from uncompressed content. The result is
deterministically sorted JSON with the existing schema and release binding.

The builder rejects an unknown `lib/` layout rather than silently
omitting app files, rejects duplicate case-insensitive ZIP paths,
unsafe paths and symbolic link/archive entry types. It limits the
number of ZIP entries, decompressed file size, total expanded bytes and
resulting inventory bytes. It never extracts files to disk and never
stores or uses a signing private key. An approved release pipeline must
sign the exact generated JSON bytes; the pinned CMS inventory reader
then validates these bytes before allowing any comparison.

A generated list covers `lib/app/` **archive payload members**. It does
not yet prove coverage of installer-created files, application shortcuts,
permissions, registry entries, external state, or the complete
post-installation ownership model. A valid CMS signature is not a
repair authorization.

The hosted-Windows signed Velopack E2E now generates a candidate
inventory from the actual verified V2 nupkg, signs it with an ephemeral
LAB certificate, verifies the pinned identity and compares the
installed `current` payload files with their expected hashes. Production
signing and recovery authorization remain disabled.

## Windows Velopack relocated root binaries

A real hosted-Windows E2E identified two generated nupkg payloads
that are **not** installed below `current/`: `lib/app/Squirrel.exe`
becomes the root `Update.exe`, and
`lib/app/<App>_ExecutionStub.exe` becomes root `<App>.exe`.
The inventory generator now maps these exact filename shapes to
their installed root paths **without dropping their hash entries**.
All other `lib/app/` members remain mapped under `current/`.
This allows the signed inventory to assess both regular payloads
and relocated launcher/updater binaries.

The mapping is a Velopack Windows packaging convention, not a
general-purpose archive extraction path rule. Any unsupported
or unexpected app layout must fail closed instead of being silently
excluded. The actual installed bytes and all changed paths still
need hosted-Windows E2E acceptance.

## Read-only installed current scope assessment

`InstalledPayloadScopeTool` enumerates only normal, non-reparse files
beneath the existing `current/` directory. It checks each ancestor,
refuses symlinks/reparse points and ambiguous names, and limits the
maximum traversal size. Access failures propagate instead of silently
producing an empty ownership report.

`InstallerPayloadScopeAssessmentService` takes the **caller-provided**
signed baseline identity and expected files and first checks its
package ID, version, channel and install-root path against the committed
`InstalledPackageState`. The invoking caller is responsible for deriving
the baseline values from `SignedPayloadInventoryReader` after CMS
verification. An identity or target mismatch fails before observing any
files. The service then produces:
- existing per-file SHA-256 evidence for every expected installed file,
  including root `Update.exe` and the launcher;
- a sorted, case-insensitive set difference identifying **unattributed**
  files actually observed inside `current/`.

**Unattributed does NOT mean malicious or safely removable.** It may
contain legitimately generated application files. `UserData`,
package caches, staging directories and other root files are out of
scope; the scanner neither traverses nor claims ownership of them.
An empty unattributed list does not prove full installation ownership,
correct ACLs, completeness across the root, or an atomic disk snapshot.
No result is an authorization to delete, repair, retry, rollback or
clear a recovery journal.

Unit tests assert read-only behavior, state/target mismatch rejection,
symlink refusal, and separate treatment of unknown `current/` files
and preserved root `UserData`. The hosted-Windows Velopack fixture also
records the number of unattributed files without treating it as an
automatic recovery decision.

## Local pilot: read-only CLI command

`TurkuazInstaller.Cli.exe audit` is now an explicit offline pilot
entry point. It uses the installed application's persisted state,
the ordinary pinned CMS-signed release-manifest reader, and the
separately pinned CMS-signed inventory reader. It refuses
missing/mismatched committed state and verifies the complete inventory
identity before accessing installed application files.

The command emits a bounded JSON diagnostic report. It returns nonzero
for missing/mismatched expected files, unattributed `current/` files,
missing signatures or other trust errors. It does not initialize the
mutating workflow, operation journal, update engine, or reboot scheduler.
Even a successful audit grants **no** rollback or journal unlock authority.

See `docs/CLI.md` for the offline command and prerequisites.

### Recovery hold cannot look clean in CLI audit

The read-only CLI now uses the existing
`InstallerRecoveryAssessmentService` against the same installed state
and operation-journal storage used by normal runtime. An observed
journal of **any** phase, an unreadable journal, or a metadata conflict
prevents a successful audit. It checks before any installed payload is
read and again after the file assessment; committed version/channel/
target must also be unchanged at the end. These are two observations,
not an atomic filesystem snapshot, operation lock, or proof of safe
automatic recovery. The CLI never saves or clears the journal.

## CI-only real-product full-package inventory probe

The `tests/TurkuazInstaller.Inventory.LabProbe` executable is an
**unsigned-inventory derivation tool**, deliberately outside production
installer builds. It requires package ID, channel, pinned signed
manifest path, independent external trust policy path, full
`-full.nupkg` file and new output path.

The probe resolves the real CMS-signed release with the production
`FileReleaseProvider` and `CmsManifestSignatureVerifier`. Only after
trusted release identity and full archive size/SHA-256 match does it
invoke `VelopackPayloadInventoryBuilder` to hash **all** mapped
`lib/app` payload files. It never signs, invents or downloads trust,
changes install state, or enables repair; it refuses to overwrite an
existing output.

TurkuazOffice's Draft Velopack pilot uses this same probe on genuine
Office packages, then signs the raw JSON bytes using only an ephemeral
LAB CMS key and asks the real CLI `audit` to prove matching on-disk
files, corruption detection, repair restoration and V1 rollback
evidence. This remains a cross-repository CI acceptance experiment,
**not** production signing, full root ownership or an atomic file
snapshot.
