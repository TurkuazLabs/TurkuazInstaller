# 📄 Dosya Yolu: /docs/SIGNED_PAYLOAD_INVENTORY.md
# 📌 Amac: Imzali dosya SHA-256 envanteri formati ve recovery icin trust sinirlarini tanimlar
# 📌 Modul - Documentation Markdown
# Version: 1.0.0
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
