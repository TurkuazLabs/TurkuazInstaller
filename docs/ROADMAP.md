# 📄 Dosya Yolu: /docs/ROADMAP.md
# 📌 Amac: TurkuazInstaller Community gelistirme fazlarini ve kabul kriterlerini takip etmek
# 📌 Modul - Markdown
# Version: 1.0.3
# Aciklama: Stable Community runtime completion, bootstrap/E2E ve production signing gate durumunu ayri takip eder
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
- [ ] bootstrap -> WinUI launch orchestration
- [ ] bootstrap self-update start orchestration
- [ ] real Velopack install/update/repair/rollback/uninstall E2E
- [ ] x64/ARM64 release strategy finalization

## v1.0.0 - Release Supply Chain

- [x] deterministic publish validation
- [x] deterministic release ZIP
- [x] GitHub artifact attestation
- [x] production signing bootstrap tooling
- [x] immutable GitHub OIDC design
- [x] production signing preflight workflow
- [ ] Azure identity/certificate registration
- [ ] Production Signing Preflight green
- [ ] signed v1.0.0 tag workflow green
- [ ] GitHub Release published

## v1.1 Sonrasi

- [ ] CLI View/Controller
- [ ] private GitHub/Gitea credential adapters
- [ ] installed-app catalog/list UI
- [ ] richer update discovery UX
- [ ] optional delta optimization behind Package Engine
