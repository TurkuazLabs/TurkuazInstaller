<!--
Dosya Yolu: REPO_ROOT/docs/ANTIVIRUS_EVIDENCE_LAB.md
Amac: Antivirüs vakalarinda salt okunur dosya/imza kaniti toplama proseduru
Modul - Markdown
Version: 1.1.0
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

## Human-recorded alert evidence

Record antivirus vendor, engine and signature-db version, exact alert/threat name, action (blocked/quarantined/restriction group), file path, time, artifact/build commit SHA, and the SHA-256 from the local collector. Redact user names, personal paths and secrets before sharing. An application restriction group classification alone is **not** proof of a Trojan detection. Compare checksums before investigating a false-positive appeal; unexpected hashes are a supply-chain or artifact mismatch until resolved.

`Get-AuthenticodeSignature` is separate from the Installer's `WinVerifyTrust` verifier. Neither this collector nor unsigned LAB packaging validates real installation lifecycle. Signer identity, full certificate chain, revocation/offline handling, publisher trust, signed CMS manifest, installation/update/repair/rollback and Windows 10/11 VM acceptance remain separate requirements.

## Status

- Collector source imported onto a main-based security branch; native Windows runtime test **NOT RUN**.
- Antivirus vendor/detection/result **NOT RECORDED**.
- Code signing and release approval **NOT ACCEPTED**.
