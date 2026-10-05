# 📄 Dosya Yolu: /docs/ROADMAP.md
# 📌 Amac: TurkuazInstaller Community gelistirme fazlarini ve kabul kriterlerini takip etmek
# 📌 Modul - Markdown
# Version: 1.0.4
# Aciklama: Stable Community kod tamamlanma durumunu, production signing dis bagimliliklarini ve sonraki minor surumleri ayri takip eder
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

## v1.1 Sonrasi

Bunlar v1.0.0 Stable Community blocker degildir:

- [ ] CLI View/Controller
- [ ] private GitHub/Gitea credential adapters
- [ ] installed-app catalog/list UI
- [ ] richer update discovery UX
- [ ] optional delta optimization behind Package Engine
- [ ] native ARM64 distribution
