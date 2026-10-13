# 📄 Dosya Yolu: /docs/SIGNED_PAYLOAD_INVENTORY.md
# 📌 Amac: Imzali dosya SHA-256 envanteri formati ve recovery icin trust sinirlarini tanimlar
# 📌 Modul - Documentation Markdown
# Version: 1.2.0
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
