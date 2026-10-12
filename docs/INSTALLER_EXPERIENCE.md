# 📄 Dosya Yolu: /docs/INSTALLER_EXPERIENCE.md
# 📌 Amac: Tum projelerin ortak kurulum arayuzunu ve yerel tanitim kartini tekrar kod yazmadan kullanmasini saglar
# 📌 Modul - Documentation Markdown
# Version: 1.1.0
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

## Release and acceptance gates

Automated checks must cover YAML defaults/disable/override and contract scope
denials. Native Windows CI must build WinUI markup, ensure the promotion panel
does not obscure progress/cancel/recovery, and test contrast at high DPI.
The actual Windows 10/11 product onboarding must verify that the **signed**
TurkuazInstaller path (not raw Velopack Setup.exe) launches and installs.

Track remaining wizard and machine-wide work in
https://github.com/TurkuazLabs/TurkuazInstaller/issues/53 .
