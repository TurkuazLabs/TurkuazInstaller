# 📄 Dosya Yolu: /docs/INSTALLER_EXPERIENCE.md
# 📌 Amac: Tum projelerin ortak kurulum arayuzunu ve yerel tanitim kartini tekrar kod yazmadan kullanmasini saglar
# 📌 Modul - Documentation Markdown
# Version: 1.9.0
# Aciklama: Ortak WinUI deneyimi, scope gelistirme kapisi, YAML profili ve tanitim davranisi
# Bagimli Oldugu Katman: Controller | Service | Repo | Tool | View | Language | Config

# Shared installer experience

TurkuazInstaller is the *only* owner of the installation UI. Products such as
TurkuazOffice, UniZip and other projects provide branding and signed release
metadata; products **do not** copy installer views or installer workflow code.

## Product branding and first-party promotion

The shared WinUI status/progress area now has a native, local, first-party
showcase panel. It uses the same product-neutral typed profile as the rest of
the UI. The default card shows the TurkuazLabs ecosystem; products can override
its text with `%LOCALAPPDATA%/TurkuazInstaller/config/ui-profile.yml`.

Example:

```yaml
# 📄 Dosya Yolu: /examples/ui-profile.yml
# 📌 Amac: Tum projeler icin ortak kurulum profili ornegi
# 📌 Modul - Config YAML
# Version: 1.2.0
# Aciklama: Uygulamaya ozel baslik ve yerel tanitim alani
# Bagimli Oldugu Katman: Config | Repo | View | Language

schema_version: 1
branding:
  window_title: Turkuaz Installer
  header_title: Turkuaz Office Kurulumu
promotion:
  enabled: true
  eyebrow: TURKUAZ EKOSISTEMI
  title: TurkuazLabs uygulamalarini kesfedin
  description: Office ve UniZip ile daha fazlasini yapin.
  provider: TurkuazLabs Community
```

`promotion.enabled: false` turns the entire panel off. Text is rendered
locally; the installer does not load network images, fetch advertising URLs,
track viewers, open websites, or change the actual installation progress.
Promotion errors may not bypass manifest trust or artifact verification.

**Scope:** This panel belongs to the TurkuazInstaller *WinUI front end*, not to
the raw Velopack one-click `Setup.exe`. The Office LAB pilot currently uses raw
Velopack, so adopting this UI is a separate signed-manifest integration task.

## Quick install vs custom install

The native WinUI Install button now opens a confirmation dialog with Quick
and Custom modes and the installation scope options.

- Quick is selected by default, clears any manual target override and uses
  the **signed manifest** default target. The target is checked against the
  current user's LocalAppData location once the release is resolved.
- Custom reveals a target field; current policy restricts it to the current
  user's LocalAppData subtree and rejects traversal outside it.
- Both modes show the scope selection; `PerMachine` is visible but disabled
  until the real machine-wide pipeline is accepted.
- The dialog's secondary action exits without installing. Its primary
  action delegates to Controller -> Service -> signed Runtime.

Both modes must eventually support choosing `PerUser` or `PerMachine`.
Quick install skips optional shortcut/path pages, never the scope decision.
Use `InstallerInstallScope` enum and `InstallerInstallScopePolicy`, not string
path prefix checks. Current runtime is per-user only; machine-wide selection
**fails closed** until reviewed UAC elevation, MSI package deployment, signed
machine-wide integration receipts and Windows multilogon lifecycle tests pass.
The current release must not expose a fake functional Program Files button.

- PerUser: installer-managed LocalAppData location; no admin elevation.
- PerMachine: Program Files with admin elevation, real all-users ownership,
  rollback, maintenance, shortcuts and uninstall; not implemented yet.
- Installer remains fast. No artificial wait or video/advertising gating.
- Prior NSIS installations are not silently replaced.

## Shared Windows MSI scope packaging probe

The optional Windows MSI Scope Packaging Probe builds both `PerUser` and
`PerMachine` MSI installers from one controlled .NET fixture using the same
pinned `vpk 1.2.161` configuration as the existing Velopack E2E.

It verifies that MSI artifacts are nonempty OLE compound-file packages and
records their SHA-256 values. It does not publish unsigned MSI executables;
only the textual package report is retained as a short-lived GitHub artifact.

A separate **LAB ONLY** step on the disposable GitHub-hosted Windows runner
attempts `PerMachine` MSI installation under a unique Program Files child,
checks the fixture and then uses official `msiexec /x` for removal. It refuses
to run outside GitHub's Windows CI host and requires an administrator token.
Timeout, nonzero exit and residual executable checks fail closed. This test
modifies only the temporary Windows runner, never customer machines or
existing NSIS installations.

The first two E2E attempts failed: [#38213314813](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/38213314813)
timed out during MSI invocation, and the second diagnostic job confirmed that
install logging had not started. Explicit quotation of the `VELOPACK_INSTALLDIR`
property value fixed the command-line parsing.

[Windows MSI run #38222596579](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/38222596579)
passed **both** PerUser (AppData) and PerMachine (Program Files) actual
install, executable/version-marker verification and MSI uninstall using a
single reusable `Test-MsiScopeLifecycle.ps1` Tool script driven by central
`config/velopack-e2e.psd1`.

[Windows MSI run #38223116584](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/38223116584)
additionally validated the registered Windows Installer product assignment
type using the native Windows Installer COM automation API:
`AssignmentType=0` for PerUser and `AssignmentType=1` for PerMachine.
Both installed product versions were then successfully uninstalled.
This closes the LAB *packaging + basic install/uninstall + registry context*
gate, **not** the production system-wide installer acceptance gate.

Additional [non-admin CI #38234536955](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/38234536955)
created a temporary real standard Windows user account. On the GitHub-hosted
Windows Server environment, this user's PerUser MSI attempt was **blocked
by Windows Installer policy 1625** ("non-assigned apps are disabled for
non-admin users"). The test did not weaken or modify this policy.

The same non-admin account **successfully installed and uninstalled**
the Velopack PerUser `Setup.exe`. The tests now load the local account's
profile, resolve its AppData from the authenticated Windows SID, and
reject inherited `runneradmin` paths; a prior test failure demonstrated
why explicit profile isolation is required.

The next LAB gate uses **two independent non-admin accounts** and one shared
Velopack fixture. A and B each install into their own SID-resolved
LocalAppData folder and write an isolated user-data marker. After A
uninstalls, B must still have its application/version and an unchanged
private data marker. A's user data must also survive A's uninstall.
A then reinstalls into the same user's AppData target while B remains
installed; A's data marker must keep its contents, and B's installation
and data must remain unchanged throughout the reinstall. Finally, both
accounts uninstall, and each account's separate data marker must survive.
Both account SIDs must differ and no child may inherit the GitHub runner
administrator profile. A failed phase fails the job; the tool attempts
cleanup in a finally block.

This is automated as `tools/Test-VelopackMultiUserChild.ps1`, orchestrated
by `tools/Test-MsiStandardUser.ps1`, with a shared Velopack process
adapter to avoid copying process/timeout logic. No production binaries,
HKLM configuration or user security policies are modified.

This establishes PerUser Velopack one-click install/uninstall on a
non-admin CI account. It does **not** establish PerUser MSI under restrictive
Windows Server policy, nor interactive UAC accept/deny, actual Windows 10/11
desktop onboarding, trusted signed production MSI, all-users ownership,
or update/repair/rollback. Those remain separate acceptance scenarios.
Do not enable PerMachine until
installer lifecycle callbacks, maintenance ownership, HKLM and Windows
integration policy have been verified.

Velopack issue https://github.com/velopack/velopack/issues/1040 documents
a known risk with per-machine lifecycle hook impersonation for the
`Either` MSI scope. Avoid silent escalation, and keep the actual
system-wide MSI execution in a separately reviewed privileged Tool adapter.

## MSI repair regression (experimental LAB gate)

After MSI install and assignment verification, the shared
`Test-MsiScopeLifecycle.ps1` now deliberately removes **only a version
marker inside the unique disposable test fixture**. It then attempts
Windows Installer `/fa` repair in the same scope and original target,
requiring the marker, main executable, updater and registered assignment
to remain correct before uninstalling the fixture.

[Windows MSI Scope CI #38261820518](https://github.com/TurkuazLabs/TurkuazInstaller/actions/runs/38261820518)
passed the actual **PerUser and PerMachine `msiexec /fa` repair** after
deliberate deletion of the test fixture's version marker. The CI verified
that the marker was restored with the original contents, the application's
main executable and updater survived, the Windows Installer assignment
remained `0` / `1`, and uninstall succeeded. The same run passed the
two-user non-admin uninstall/reinstall user-data isolation regression.

This is accepted **only** as basic LAB file-repair behavior for the pinned
Velopack MSI fixture. It does not prove all application binaries, signed
artifact validation during repair, privileged lifecycle hooks, production
installer UI, multi-user MSI repair or rollback. The user-facing `Onar`
workflow must still enforce signed-manifest policy and keep real user data
safe. Do not intentionally delete user binaries on production machines
for this test.

## Release and acceptance gates

Automated checks must cover YAML defaults/disable/override and contract scope
denials. Native Windows CI must build WinUI markup, ensure the promotion panel
does not obscure progress/cancel/recovery, and test contrast at high DPI.
The actual Windows 10/11 product onboarding must verify that the **signed**
TurkuazInstaller path (not raw Velopack Setup.exe) launches and installs.

Track remaining wizard and machine-wide work in
https://github.com/TurkuazLabs/TurkuazInstaller/issues/53 .
