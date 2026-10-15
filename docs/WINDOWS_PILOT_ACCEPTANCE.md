# 📄 Dosya Yolu: /docs/WINDOWS_PILOT_ACCEPTANCE.md
# 📌 Amac: Windows 10/11 gercek bilgisayarda imzasiz LAB ve imzali production kabul farklarini belgelendirir
# 📌 Modul - Documentation Markdown
# Version: 1.0.0
# Aciklama: Pilot on kontrol, paket sinirlari, kullanici verisi ve UAC/ownership kabul tutanagi
# Bagimli Oldugu Katman: Tool | Config | View

# Windows 10/11 local pilot acceptance (not production release)

## First gate: nonmutating combined-distribution preflight

Download and extract the complete `TurkuazInstaller-win-x64` artifact
from the current **Windows Desktop CI** job for the PR head.
For native ARM64 Windows use the corresponding ARM64 artifact.

From the repository root, use Windows PowerShell or PowerShell 7:

```powershell
.\tools\Test-WindowsPilotPreflight.ps1 -DistributionRoot "C:\Pilot\TurkuazInstaller" -Architecture x64
```

The tool checks the three required EXEs
(`TurkuazInstaller.Bootstrapper.exe`,
`app/TurkuazInstaller.WinUI.exe`,
`cli/TurkuazInstaller.Cli.exe`):
- nonempty executable bytes and valid PE headers;
- PE Machine is the requested x64 or ARM64 (not a mislabeled bundle);
- SHA-256 of each file and Authenticode status;
- supported Windows version (10.0.17763 or later).

It emits JSON with `diagnostic_only:true`,
`executed_installer:false` and `production_acceptance:false`.
It performs **no installs, registry writes or elevation**.

Unsigned CI packages are LAB artifacts. `NotSigned` is expected;
do not bypass SmartScreen, corporate endpoint controls, security policy,
or antivirus to use them on an untrusted or work device. Prefer an
isolated disposable test VM. Production validation must instead pass
`-RequireSigned`, and this merely checks signatures; it does not
replace signed release-manifest or publisher-pin policy verification.

## Second gate: manual non-admin per-user pilot

Use a disposable clean Windows 10/11 VM with a dedicated local
non-admin account, a VM snapshot, and a harmless sample product
signed manifest with an independently provisioned test trust pin.
Before trying the installer, record OS build, processor architecture,
account privilege level, VM snapshot identifier and pilot artifact
commit SHA. Do **not** target Program Files, an existing NSIS
installation, an existing product directory or real user data.

Record each result in the pilot log:
1. Startup: bootstrapper and WinUI open correctly at normal user privilege;
   exiting the wizard before install creates no installed application.
2. Per-user quick/custom selection: signed manifest target stays within
   that user's LocalAppData; blocked paths and per-machine selection
   remain rejected without changing files.
3. Install V1: signed manifest trust, artifact SHA-256, version, shortcuts
   and saved state are verified.
4. Place unique harmless `UserData` bytes outside `current/`.
5. Update V1 -> V2, repair V2 and rollback V2 -> V1 using independently
   verified release metadata; user-data bytes remain unchanged.
6. Uninstall: application payload is removed by the official workflow,
   committed state is reconciled and product-specific preserved data
   remains untouched as required by product policy.
7. Negative tests on an **isolated second VM snapshot**: missing/incorrect
   CMS signature, wrong publisher pin, digest mismatch, unexpected
   interruption checkpoint and insufficient privileges all fail closed.
   Never intentionally corrupt or kill a live production install.
8. Signed inventory `audit` with exact version-specific local manifest
   and `.p7s` files: verify correct hash report; changing a disposable
   test file produces a nonzero result without mutations.
9. UAC cancel and permission denial: never silently elevate or partially
   deploy machine-wide files. Do not enable the disabled PerMachine UI
   for this pilot.

## Release sign-off blockers

A green CI or pilot run does **not** prove the following:
- Production Azure Artifact Signing or another approved public-trust
  signing certificate, verified signer identity, OIDC configuration and
  a successful production signing preflight.
- Release signing and manifest/inventory publisher-pin provisioning.
- Actual Windows 10 and 11 interactive UAC accept/deny, non-admin and
  multilogon ownership, safe OS integration maintenance.
- Signed recovery artifacts, atomic ownership/ACL claims, safe interrupted
  updater reconciliation, explicit recovery approval and preservation.
- Antivirus/SmartScreen verdict and release download verification.

`PR #54` stays Draft; do not tag an unsigned artifact as Stable or
enable unattended rollback, repair, journal deletion or machine-wide
installation solely because this checklist is present.
