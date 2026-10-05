# 📄 Dosya Yolu: /docs/ROADMAP.md
# 📌 Amac: TurkuazInstaller Community gelistirme fazlarini ve kabul kriterlerini takip etmek
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Stable Community kod ve production release gate durumunu ayri olarak takip eder
# Bagimli Oldugu Katman: Controller | Service | Repo | Tool | View | Language | Config

# Roadmap

## v0.2.0 - Contract Foundation

- [x] Community / Pro dependency direction
- [x] Installer manifest contract
- [x] Release provider contract
- [x] Community security model
- [x] Architecture boundary
- [x] Contract validator CI
- [x] Domain project
- [x] Application project
- [x] Port interfaces

## v0.3.0 - Core Domain

- [x] typed package/version/channel models
- [x] install/update/repair/rollback plans
- [x] verification result model
- [x] unit tests

## v0.4.0 - Providers

- [x] GitHub adapter
- [x] Gitea adapter
- [x] generic HTTPS adapter
- [x] local file adapter
- [x] provider contract tests

## v0.5.0 - Package Engine

- [x] package engine port
- [x] Velopack adapter
- [x] atomic staging
- [x] apply
- [x] repair
- [x] rollback
- [x] process runner port
- [x] package engine contract tests

## v0.6.0 - Windows Bootstrap

- [x] NativeAOT bootstrapper
- [x] prerequisite detection
- [x] self-update handoff
- [x] process elevation boundary
- [x] Windows NativeAOT CI

## v0.7.0 - WinUI 3

- [x] ViewModel
- [x] install/update/repair/rollback UI
- [x] progress/events
- [x] error/recovery UX
- [x] real Application workflow wiring
- [x] HTTPS/file artifact download
- [x] SHA-256 runtime verification
- [x] JSON install state repository
- [x] self-contained WinUI Windows CI

## v1.0.0 - Stable Community Code

- [x] signed release pipeline
- [x] reproducible package validation
- [x] installer recovery tests
- [x] security review
- [x] documentation

## v1.0.0 - Production Release Gate

- [ ] Azure Artifact Signing OIDC identity connected
- [ ] production certificate profile configured
- [ ] Stable Readiness CI green on release commit
- [ ] signed v1.0.0 tag workflow green
- [ ] GitHub Release published
