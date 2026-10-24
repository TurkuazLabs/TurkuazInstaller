<!--
Dosya Yolu: REPO_ROOT/docs/ANTIVIRUS_EVIDENCE_LAB.md
Amac: Antivirüs vakalarinda salt okunur dosya/imza kaniti toplama proseduru
Modul - Markdown
Version: 1.5.0
Aciklama: LAB kaniti ile malware karari ve production kabulunu ayirir
Bagimli Oldugu Katman: Tool | Security | Testing
-->

# Antivirus Evidence LAB (NOT ACCEPTED)

Tracking: [Issue #58](https://github.com/TurkuazLabs/TurkuazInstaller/issues/58).

This is a **diagnostic procedure**, not false-positive proof, malware clearance, Windows Defender allowlisting or production signing acceptance. Do not disable antivirus, SmartScreen, revocation checks or CMS manifest checks.

## Input and protection

Use a disposable Windows 10/11 VM with antivirus protection **enabled**. Obtain the exact file from a validated short-lived CI LAB artifact and check its published SHA-256 independently. Do not execute a quarantined or suspected-malicious binary. The collector reads file metadata, hash and native Windows Authenticode via PowerShell; it does not execute the artifact.

## Collect one file at a time

PowerShell 7 on Windows; create a private output directory before running:

```powershell
$Repo = 'C:\Lab\TurkuazInstaller'
$Artifact = 'C:\Lab\Payload\TurkuazInstaller.Bootstrapper.exe'
$Output = 'C:\Lab\Evidence\REV-01.json'
# Compare the actual file against an independently validated CI artifact checksum.
$ExpectedSha256 = '<actual-64-character-SHA256-from-verified-artifact>'
& (Join-Path $Repo 'tools\diagnostics\Get-AuthenticodeLabEvidence.ps1') -ArtifactPath $Artifact -CaseId REV-01 -OutputPath $Output -ExpectedSha256 $ExpectedSha256
```

These are illustrative VM paths; use **actual** existing paths on your VM. The collector refuses existing output files, verifies any supplied 64-character expected SHA-256 before signature inspection, rechecks that the file hash did not change, and does not modify installation state. A mismatch fails without writing JSON. The `-ExpectedSha256` option is optional for existing LAB workflows; when omitted, `matches_expected_sha256` is `null`, not `true`. The expected checksum **must** come from separately validated CI provenance, not the same suspect file. `release_acceptance=NOT_ACCEPTED` always remains. A successful execution only confirms diagnostic evidence was written.

## Link each real EXE to the hosted Windows build

Windows Desktop CI now includes a **separate, short-retention LAB diagnostic artifact**
for the three real output binaries. It is never placed into the combined
distribution or a production release:

- `TurkuazInstaller-TEST-EXE-EVIDENCE-win-x64`: `installer-exe-x64.json`
- `TurkuazInstaller-TEST-EXE-EVIDENCE-win-arm64`: `installer-exe-arm64.json`

The actual **unsigned CI distribution** artifacts now have explicit names
`TurkuazInstaller-TEST-ONLY-UNSIGNED-DISTRIBUTION-win-x64`
and `TurkuazInstaller-TEST-ONLY-UNSIGNED-DISTRIBUTION-win-arm64`.
Both are short-lived (3 days). These are **CI test outputs** and are
distinct from production-signed GitHub Release ZIP assets. Historical
CI run artifacts created before the rename may retain ambiguous legacy
names and longer retention; they must never be treated as signed releases.

The generator (`tools/diagnostics/New-InstallerExecutableInventory.ps1`)
reads only these fixed distribution paths:
`TurkuazInstaller.Bootstrapper.exe`,
`app/TurkuazInstaller.WinUI.exe`, and
`cli/TurkuazInstaller.Cli.exe`.
It records individual file sizes, SHA-256 digests, Windows Authenticode
statuses and a **caller-supplied, unverified** source commit marker. The
generator fails on missing/empty files, reparse points, file hash changes,
existing outputs or outputs inside the distribution. It **does not run or
scan any EXE**.

Download the exact distribution and evidence artifacts from the **same
successful Windows Desktop workflow run** and check the GitHub workflow's
branch/commit/actor first. Validate the downloaded archive against trusted
release/CI checksums or attestation if available. Do **not** treat the
JSON and EXE from the same untrusted location as independent proof of
authenticity; the JSON is diagnostic identity metadata, **not signed
provenance**. If either artifact is unavailable, record `NOT_RECORDED`
rather than fabricating a hash.

For manual AV investigation, select the appropriate `relative_path`
from the trusted run's JSON and supply that entry's `sha256` to the
single-file collector's `-ExpectedSha256` option. Confirm each exact EXE
identity independently, not merely the enclosing ZIP checksum. A valid
identity comparison is **not** an antivirus-clean, SmartScreen-reputation
or production-signing verdict.


## Firefox blocked a GitHub Actions ZIP: archive and EXE are different identities

On 2026-10-24, Firefox displayed `Bu dosya virüs veya zararlı yazılım içeriyor`
for the **Ragnar QA GitHub Actions ZIP**. That is a browser download
block, **not** an independently established malware diagnosis or false-positive
finding. A second Firefox entry said the file was moved/deleted: this is
not a second clean/malicious scan verdict. The Kaspersky application showing
no warning does not override Firefox, or establish AV clearance.

Keep ZIP and EXE digests separate. Confirmed metadata for the affected
**pull-request run** [40086998803](https://github.com/b1glord/ragnarokclientpatch/actions/runs/40086998803)
(artifact ID `12557547786`) is:

| Object | Digest source | SHA-256 |
| --- | --- | --- |
| GitHub Actions outer ZIP | GitHub artifact API metadata | `dbbc58fb68b29f0509213a62469509b530ec07c82aa0b471d56dfddeba6b9f14` |
| `RagnarClassic-Turkuaz-Setup.exe` ZIP member | Same-run Windows build log | `93534602ad93009f33728a6e68e43cfeb28f4e4c1199bd19c5f9b9bbb406960f` |

The separate **push run** `40086995530` produced a different archive
digest and EXE digest. Never compare or submit a ZIP hash as an EXE hash.
These digests are historical identifiers from the referenced QA run;
they are **not** signatures or assurances of safety, and no blocked
download was bypassed to obtain them.

The standalone, offline `tools/diagnostics/verify_archive_provenance.py`
checks the outer ZIP **and explicitly selected members** against
independently obtained expected hashes, without extracting or executing
anything. It reads selected entries with a decompression size limit, rejects
unsafe names/symlink or duplicate ZIP records, and atomically creates a
diagnostic report. It never calls an AV engine and always records
`browser_verdict=NOT_RECORDED` and
`production_acceptance=NOT_ACCEPTED`.

**Only if a copy is already safely available in an isolated investigation
environment**, use Python 3 and replace the example paths with actual
local paths. Do not override Firefox, restore a quarantined file, or run it
merely to collect evidence:

```powershell
# Illustrative local paths: create C:\Lab\Evidence beforehand.
python tools/diagnostics/verify_archive_provenance.py `
  --archive "C:\Lab\Downloads\RagnarClassic-Turkuaz-Single-EXE-TEST-ONLY.zip" `
  --expected-archive-sha256 "dbbc58fb68b29f0509213a62469509b530ec07c82aa0b471d56dfddeba6b9f14" `
  --expected-member "RagnarClassic-Turkuaz-Setup.exe=93534602ad93009f33728a6e68e43cfeb28f4e4c1199bd19c5f9b9bbb406960f" `
  --output "C:\Lab\Evidence\ragnar-zip-member-identity.json"
```

If Firefox blocked the archive and no safe copy exists, record
`DOWNLOAD_BLOCKED` and the reported warning; **do not** claim local
ZIP/member verification. Record Firefox version, the warning text, the
exact artifact run ID, timestamp, whether the download was blocked or
quarantined, vendor/engine findings, and any browser-specific diagnostic
classification available without reducing protection. Distinguish
download reputation, malware/phishing classification, and endpoint AV
results; do not infer the underlying Safe Browsing vendor verdict from
a screenshot alone.

If a scan engine positively identifies the file, investigate the
**exact ZIP/EXE hash** and the internal Velopack payload before any
false-positive submission. If the hashes match and the code/build chain
has been independently reviewed, a provider-specific false-positive
review may be opened. Do not apply browser exceptions, disable
endpoint protection, obscure/pack binaries to avoid detection, or
substitute an unsigned test package for an approved signed release.

## Human-recorded alert evidence

Record antivirus vendor, engine and signature-db version, exact alert/threat name, action (blocked/quarantined/restriction group), file path, time, artifact/build commit SHA, and the SHA-256 from the local collector. Redact user names, personal paths and secrets before sharing. An application restriction group classification alone is **not** proof of a Trojan detection. Compare checksums before investigating a false-positive appeal; unexpected hashes are a supply-chain or artifact mismatch until resolved.

`Get-AuthenticodeSignature` is separate from the Installer's `WinVerifyTrust` verifier. Neither this collector nor unsigned LAB packaging validates real installation lifecycle. Signer identity, full certificate chain, revocation/offline handling, publisher trust, signed CMS manifest, installation/update/repair/rollback and Windows 10/11 VM acceptance remain separate requirements.

## Distinguish actual detection from reputation or policy restriction

**Microsoft Defender:** In a disposable Windows 10/11 VM with protection enabled, open Event Viewer (`eventvwr.msc`) and navigate to **Applications and Services Logs > Microsoft > Windows > Windows Defender > Operational**. Review the event timestamps matching the test artifact.

| Event ID | Interpretation |
| --- | --- |
| 1116 | Defender detected malware or potentially unwanted software |
| 1117 | Defender recorded a protection/remediation action |
| 1118 / 1119 | Remediation failure/critical failure; requires review |
| 1015 | Behavioral suspicion, not itself evidence of a particular EXE malware verdict |

Microsoft documentation: [Defender Antivirus events and error codes](https://learn.microsoft.com/en-us/defender-endpoint/troubleshoot-microsoft-defender-antivirus). Events can contain sensitive user names and absolute file paths. **Do not export/upload unredacted event XML or entire logs.** Copy only the event ID, time, threat name, action and engine/signature versions after manual redaction. If Defender is passive because a third-party antivirus is active, absence of events is **NOT** a clean detection result.

**Kaspersky:** Capture the exact event type and action from its protection/report UI, engine/database version, detected file identity and threat label. A low/restricted application trust group alone is **not** a malware detection. Do not treat SmartScreen reputation or "unknown publisher" messages as Defender malware IDs. Do not alter antivirus exclusion lists, trust groups, quarantines, security policy or scanning settings to achieve a passing test.

Correlate manually by exact file SHA-256, trusted artifact provenance, time, binary identity and AV verdict. If the file has been quarantined, **do not restore or run it** to obtain a hash. Use only the already-validated CI artifact hash and the AV's reported details. A mismatched hash is **not** eligible for false-positive acceptance. Manufacturer/vendor false-positive review requires independently checked hash/provenance and vendor-specific evidence; no automatic vendor submission is performed.

## Status

- Read-only collector behavior passed hosted Windows 2025 regression at commit `8fafe037c7e2b7ce01fdc16360fb1b17a4a9b880`; this **does not** exercise an end-user Windows VM or any Defender/Kaspersky real scan.
- Exact-current-head CI must be checked separately after every new commit. A successful PowerShell regression is **NOT** an antivirus clean verdict.
- Real Windows 10/11 VM antivirus verdict and vendor/detection evidence: **NOT RECORDED**.
- Production code signing, native WinVerifyTrust/revocation acceptance and release approval: **NOT ACCEPTED**.
