# 📄 Dosya Yolu: /docs/ROADMAP.md
# 📌 Amac: TurkuazInstaller Community gelistirme fazlarini ve kabul kriterlerini takip etmek
# 📌 Modul - Markdown
# Version: 0.2.0
# Aciklama: Contract foundation'dan Windows installer runtime ve stable release'e kadar teknik sirayi tanimlar
# Bagimli Oldugu Katman: Domain | Application | Port | Adapter | ViewModel | View | Config

# Roadmap

## v0.2.0 - Contract Foundation

- [x] Community / Pro dependency direction
- [x] Installer manifest contract
- [x] Release provider contract
- [x] Community security model
- [x] Architecture boundary
- [ ] Contract validator CI
- [ ] Domain project
- [ ] Application project
- [ ] Port interfaces

## v0.3.0 - Core Domain

- typed package/version/channel models
- install/update/repair/rollback plans
- verification result model
- unit tests

## v0.4.0 - Providers

- GitHub adapter
- Gitea adapter
- generic HTTPS adapter
- local file adapter
- provider contract tests

## v0.5.0 - Package Engine

- package engine port
- Velopack adapter
- staging
- apply
- repair
- rollback

## v0.6.0 - Windows Bootstrap

- native bootstrapper
- prerequisite detection
- self-update handoff
- process elevation boundary

## v0.7.0 - WinUI 3

- ViewModel
- install/update/repair/rollback UI
- progress/events
- error/recovery UX

## v1.0.0 - Stable Community

- signed release pipeline
- reproducible package validation
- installer recovery tests
- security review
- documentation
