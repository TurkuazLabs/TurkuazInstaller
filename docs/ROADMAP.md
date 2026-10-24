# 📄 Dosya Yolu: /docs/ROADMAP.md
# 📌 Amac: TurkuazInstaller Community gelistirme fazlarini ve kabul kriterlerini takip etmek
# 📌 Modul - Markdown
# Version: 2.4.0
# Aciklama: Stable Community, safe delta optimization ve native ARM64 dagitim tamamlama durumunu takip eder
# Bagimli Oldugu Katman: Controller | Service | Repo | Tool | View | Language | Config

# Roadmap

## v0.2.0 - v0.7.0 Foundation

- [x] contract-first Domain/Application/Port
- [x] GitHub, Gitea, HTTPS ve local file providerlari
- [x] Velopack package engine
- [x] atomic staging
- [x] NativeAOT bootstrap baseline
- [x] WinUI 3 desktop
- [x] install/update/repair/rollback
- [x] JSON state repository
- [x] recovery UX

## v1.0.0 - Runtime Completion

- [x] manifest install policy runtime mapping
- [x] full install mode contract
- [x] Windows prerequisite runtime probe
- [x] preserve_paths runtime wiring
- [x] rollback manifest policy enforcement
- [x] optional Authenticode artifact verification
- [x] uninstall Domain -> Service -> Repo -> Tool -> View
- [x] manifest-driven default target
- [x] progress ordering race fix
- [x] bootstrap -> WinUI launch orchestration
- [x] bootstrap self-update start orchestration
- [x] real Velopack install/update/repair/rollback/uninstall E2E
- [x] release architecture strategy: Stable v1 win-x64, ARM64 sonraki minor surum

## v1.0.0 - Distribution Quality

- [x] combined bootstrap + app/WinUI distribution
- [x] Core CI
- [x] Contract Validation
- [x] Windows Desktop CI
- [x] Stable Readiness CI
- [x] Velopack E2E
- [x] deterministic publish validation
- [x] deterministic combined release ZIP
- [x] GitHub artifact attestation tooling
- [x] recovery testleri
- [x] security review

## v1.0.0 - Release Supply Chain

Kod ve workflow gelistirmesi tamamlandi.

Asagidaki maddeler repository gelistirmesi degil, production Azure/GitHub hesap konfigurasyonudur:

- [x] production signing bootstrap tooling
- [x] immutable GitHub OIDC design
- [x] production signing preflight workflow
- [ ] Azure identity/certificate registration
- [ ] Production Signing Preflight green
- [ ] signed v1.0.0 tag workflow green
- [ ] GitHub Release published

## v1.0.0 - Proje Gecis Hazirligi

- [x] proje entegrasyon standardi
- [x] reusable manifest template
- [x] NSIS replacement migration adimlari
- [x] full package + rollback + uninstall kabul kriterleri

## v1.1.0 - Replacement Readiness

Rakip installer analizi sonrasi NSIS'i tum projelerde kaldirmadan once tamamlanacak P1 katmani:

- [x] Authenticode publisher subject pinning
- [x] optional certificate SHA-256 pinning
- [x] package-scoped cross-process operation lock
- [x] crash/reboot operation journal
- [x] structured JSONL diagnostic log
- [x] detached signed manifest trust
- [x] prerequisite auto-install
- [x] generic prerequisite detection engine
- [x] reboot/resume orchestration
- [x] CLI/silent mode
- [x] bootstrap self-update discovery/download

## v1.2 Sonrasi

- [x] private GitHub/Gitea credential adapters
- [x] proxy support
- [x] installed-app catalog/list UI
- [x] richer update discovery UX
- [x] background update policy
- [x] version skip/pinning
- [x] safe Windows integration actions (signed shortcut/protocol policy, ownership receipt, HKCU-only registry, safe cleanup, tests ve contract)
- [x] branding/localization extensions
- [x] optional delta optimization behind Package Engine
- [x] native ARM64 distribution


## v1.3 Candidate — Signed External Theme Packs (QA only)

- [x] Reuse existing detached CMS/PKCS#7 verifier and package-scoped publisher/certificate pin.
- [x] Strict `theme.tpk` format: signed manifest + signed inventory + bounded PNG assets.
- [x] Implement fail-closed runtime reader; reject extra ZIP files, traversal, symlinks, wrong product and modified images.
- [x] Build-time OpenSSL signing utility without private keys in the repository.
- [x] Add ephemeral-CMS signature and ZIP-policy regression tests to Core CI.
- [ ] Prove a complete CI run of the latest theme-pack branch and an end-to-end real Windows GUI smoke.
- [ ] Register production publisher certificate/pin through release trust tooling.
- [ ] Wire verified assets into real WinUI/installer views without any unsigned image fallback.
- [ ] Package and verify Ragnar's three approved originals; confirm setup stages on 100/125/150% DPI.
- [ ] Add the separately installed Beam launcher theme assets to the Velopack payload.
- [ ] Evaluate optional AES-GCM encryption **only with an external key-provisioning design**; v1 `.tpk` assets remain ZIP-readable.
- [ ] Define downgrade/rollback, revocation, signing rotation and malformed-image decoding acceptance tests.

`SignedThemePackReader` is infrastructure only in this phase. Existing
installer and launcher UI do not automatically use it. Details:
[Signed Theme Pack v1](SIGNED_THEME_PACK.md).
