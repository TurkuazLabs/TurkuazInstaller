<!--
Dosya Yolu: docs/STANDARD_INSTALLER_THEME.md
Amac: Turkuaz Installer'in standart koyu/turkuaz gorsel kimligini ve kurulum UX kontratini belgelendirmek
Modul - Markdown
Version: 1.0.0-qa
Aciklama: Global, urun-bagimsiz tema ve Ragnar QA ile urun entegrasyonunun sinirlari
Bagimli Oldugu Katman: View | Configuration | Tests
-->

# Turkuaz Installer Standard Theme (QA)

**Default visual preset:** `StandardInstallerTheme`, applied to the existing
WinUI desktop window via `App.xaml`. This preset is product-independent.
Its teal-accent dark palette and branded header are adapted from the
one-window Ragnar Classic QA installer.

## Theme contract

- Source: `src/TurkuazInstaller.WinUI/themes/StandardInstallerTheme.xaml`
- Integration: `src/TurkuazInstaller.WinUI/App.xaml`
- Host: `src/TurkuazInstaller.WinUI/views/MainWindow.xaml`
- Resource keys: background, hero, panel, accent, heading, muted text,
  and primary button style, all prefixed `TurkuazInstaller`.
- The existing `ui-profile.yml` contract still owns application-specific
  text and culture overrides (title, subtitle, footer, status labels).
- This theme **does not** change package location, trusted publisher,
  manifest or signature verification, install command, cancellation or rollback.

## One-window product installer flow (target standard)

1. Open exactly one themed welcome/splash window, no second engine splash.
2. Show install location and optional desktop/Start shortcut choices inside
   that window; preserve a safe default path and fail closed on unsafe paths.
3. Show indeterminate progress if the engine does not provide trustworthy
   numeric progress. Do not fake percentage.
4. Verify the real install result and report errors in the same window.
5. Show completion and opt-in application launch (no surprise automatic run).

The existing WinUI desktop app is a **package management interface**, not
yet a one-file self-extracting setup generator. Applying this visual preset
does not claim to transplant Ragnar's native C++ setup wizard or make the
full TurkuazInstaller Core a 14 MB single EXE. A reusable front-end wizard
and engine adapter remain a separate integration task before calling the
five-stage product installer UX fully implemented in Turkuaz Core.

Ragnar QA reference:
https://github.com/LevelUpGT/beam-patcher/pull/11
The distributed Ragnar native wizard delegates to Velopack, while CI tests
the TurkuazInstaller Community engine separately. Keep this distinction
visible in documentation.

## Validation

`python -m unittest discover -s tools/tests -p 'test_standard_installer_theme.py' -v`

This source-level contract verifies the theme is loaded, keys are unique,
controller event handlers are unchanged and the one-window UX guidance is
present. Windows Desktop CI must still compile WinUI; manual visual
Windows 10/11 testing remains required for DPI, contrast, buttons, install,
cancel, uninstall and upgrade. This is a **draft QA integration**,
not a signed production installer or release.
