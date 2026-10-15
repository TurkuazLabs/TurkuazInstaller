# 📄 Dosya Yolu: /docs/PROJECT_MIGRATION_ROLLOUT.md
# 📌 Amac: NSIS/Inno yerine TurkuazInstaller'i TurkuazLabs/TurkuazSoft/LevelUpGT uygulamalarinda kontrollu yayginlastirmak
# 📌 Modul - Documentation Markdown
# Version: 1.0.0
# Aciklama: Repo-sourced migration matrix and protected phase-by-phase release rollout; no production switch
# Bagimli Oldugu Katman: Tool | Config | Release | Docs

# TurkuazInstaller – Project Migration Rollout

**Policy:** TurkuazInstaller will be the long-term shared installer and
maintenance engine for compatible Windows desktop products. **No global
NSIS/Inno removal** until each product independently passes the acceptance
gates below. Publicly released Windows setup packages must retain approved
production signing; temporary CI CMS test certificates and unsigned LAB
bundles do not authorize distribution.

Checked from repository source/README on 2026-10-15. Status is *migration
readiness*, not a claim that every application has been packaged or tested.

| Priority | Project | Observed existing deployment | Migration decision |
| --- | --- | --- | --- |
| 1 | [TurkuazOffice](https://github.com/TurkuazLabs/TurkuazOffice) | Tauri/NSIS; draft Velopack + Installer LAB integration | **Active isolated Windows pilot only.** Real Tauri V1/V2 LAB EXEs and signed CLI lifecycle passed in prior CI, but both compiled from the same source. Manual WinUI install, NSIS coexistence and production signing still required. |
| 2 | [Start3POS](https://github.com/TurkuazSoft/Start3POS) | Java 21 + Liberica JRE Full portable, ZIP and NSIS Setup EXE | **Next candidate after Office.** Prepare self-contained, portable full payload; preserve config/log/runtime policy; verify SQL Server connectivity and data ownership outside install. Never uninstall or migrate a working POS in-place during first pilot. |
| 3 | [UniZip](https://github.com/TurkuazLabs/UniZip) | Java 21 desktop built with Maven | Investigate Windows EXE/portable runtime packaging and manifest first; no assumption of existing NSIS or installer ownership. |
| 4 | [JExporter](https://github.com/TurkuazLabs/JExporter) | Java/Maven desktop, OCR/tessdata resources | Investigate complete Java/advanced OCR resources, executable entrypoint and relocation under current/, then test clean install and preserved output data. |
| 5 | [TurkuazPasswordManager](https://github.com/TurkuazLabs/TurkuazPasswordManager) | Rust desktop, current Inno Setup, signed-release gates | **High-risk migration, deferred.** Encrypted vault and settings preservation, browser native host, autotype, autostart/tray, update paths, per-user/per-machine distinctions and uninstall ownership need their own privacy/security acceptance. No automatic adoption. |
| Later | [Nova9](https://github.com/TurkuazSoft/Nova9), [JHoster](https://github.com/TurkuazLabs/JHoster) | Python runtime/worker/control plane or hosting agent | Analyze Windows service/worker identity, process supervision, startup/restart, durable state and prerequisites before claiming installer compatibility. |
| Separate | [RagnarokPacketViewer](https://github.com/LevelUpGT/RagnarokPacketViewer) | C++ Windows x86 packet tooling | Architecture adapter and x86 release compatibility review before any conversion; x64 pilot does not prove x86. |
| Not applicable | [OpenCart-OpenPOS](https://github.com/TurkuazSoft/OpenCart-OpenPOS) | PHP/OpenCart extension installed in a web server | **Do not migrate to Windows Installer.** Continue OpenCart-native packaging and upgrade lifecycle. |

## Product-by-product release gates (strict)

1. **Ownership map**: inventory current setup scripts, registry keys,
   shortcuts, file associations, services, uninstall entry, startup tasks,
   installed files, target and all persisted application/user data.
   Distinguish NSIS/Inno/other-owned resources from TurkuazInstaller-owned
   resources. Do not overwrite or delete foreign installation resources.
2. **Portable runtime**: create authentic, independently versioned Windows
   executable/payload and full Velopack nupkg, with deterministic per-product
   package ID, supported architecture and intact native dependencies. Verify
   genuine V1->V2 binary/behavior changes, not a patch-marker-only update.
3. **Manifest trust**: generate a bounded manifest and its raw-byte detached
   CMS `.p7s`; provision external package-scoped signer subject and SHA-256
   certificate pin independently from the release feed. Require artifact
   size and SHA-256; for production, approved code signing and verified
   publisher identity. Keep private signing keys out of source and artifacts.
4. **Isolated acceptance**: snapshot disposable Win10 and Win11 VMs,
   dedicated ordinary user, no antivirus or UAC bypass. Perform install,
   second-version update, repair, previous-version rollback and uninstall,
   plus preserved data verification. Test bad signature, unexpected journal,
   aborted operation, ownership conflict and insufficient privileges.
5. **Windows UI/integration**: verify GUI wizard, keyboard accessibility,
   DPI, actual Start Menu/Desktop shortcuts, application arguments,
   file associations/protocols and uninstall receipts. Neither readonly
   XAML/NSIS source checks nor a native CLI probe replace clickable GUI
   migration and registry ownership acceptance.
6. **Repeatable release**: track all dependency locks; build/provision
   signed win-x64 release packages, manifests, trust policy and rollback
   baseline through an auditable CI pipeline. Approve production signing
   preflight and final end-user verification before replacing any release
   download link or auto-update feed.
7. **Canary and rollback**: first provide opt-in beta release to a small
   test population with backup and full path back to the old installer.
   Retain the original NSIS/Inno release assets until migration/adoption
   succeeds. Switch one product at a time, never all repositories together.

## Current blocking evidence

- [Installer PR #54](https://github.com/TurkuazLabs/TurkuazInstaller/pull/54)
  is **Draft**. Its WinUI/Desktop/CLI/Velopack test baseline is green.
  The `System.Security.Cryptography.Pkcs` 10.0.13 security-patch commit
  `eec834c4` separately passed Windows Desktop, Core, Velopack E2E and
  Signing Tooling Validation on 2026-10-15. No production Artifact Signing
  account/profile approval is asserted.
- [Office PR #71](https://github.com/TurkuazLabs/TurkuazOffice/pull/71)
  remains **Draft**. The preceding portable Office LAB kit run was blocked
  by the older pinned vulnerable PKCS 10.0.12 restore before GUI kit
  preparation. The new exact pinned Installer `eec834c4` Office Windows
  run must finish green; no fresh kit download or user WinUI lifecycle
  should be claimed before the artifact actually uploads.
- Existing Office NSIS remains authoritative until manual clean and
  coexistence/ownership Windows 10/11 tests, real distinct Office
  release versions, signed public production artifact/trust provisioning
  and file-association/shortcut migration acceptance complete.

## Next concrete execution

**First:** complete the Office signed CMS portable WinUI kit and manually
verify a full **disposable VM** V1 install -> V2 update -> repair -> V1
rollback -> uninstall from the actual TurkuazInstaller GUI, including
UserData and installed state before/after.

**Second:** create a separate **Start3POS** repository migration Draft PR
with an NSIS behavior and Java/SQL/runtime prerequisite inventory. Do not
modify the installer in that repo, repoint public download links or change
a live point-of-sale installation until the read-only inventory is reviewed.

Reference policies:
- [Project Integration Standard](PROJECT_INTEGRATION.md)
- [Windows Integration Ownership](WINDOWS_INTEGRATION.md)
- [Windows Pilot Acceptance](WINDOWS_PILOT_ACCEPTANCE.md)
- [Stable Release Checklist](RELEASE_CHECKLIST.md)
