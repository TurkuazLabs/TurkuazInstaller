<!--
Dosya Yolu: REPO_ROOT/AGENTS.md
Amac: TurkuazInstaller icin aktif BURAK DEV STANDARD v3.0.0 kurallari
Modul - Documentation Markdown
Version: 3.0.0
Aciklama: Pinli merkezi standarttan turetilen public katilimciya acik yerel kontrat
Bagimli Oldugu Katman: Documentation
-->

# TurkuazInstaller - Project Agent Contract

**Standard:** BURAK DEV STANDARD v3.0.0, active.
**Pinned upstream source:** https://github.com/TurkuazSoft/DevStandards/tree/72f6f642128fa29ec9a68b18881e9aa225739c9d
**Technology:** Community installer.
**Relevant rules:** standards/testing-ci.md.
**Source of project-specific truth:** This repository's README, CONTRIBUTING, docs, issues, PRs and existing application behavior.

## Contributor-safe rules

- Inspect project source, branch, open PRs, roadmap, and CI before editing. "Devam et" resumes verified work, without inventing completion.
- Protect existing user changes, compatibility, security, and data. No unrelated refactor, unapproved release, or live DB change.
- Logical architecture: Controller orchestration; Service business rules; Repository/Model storage; Tool/Adapter external systems; View presentation; Language localization. Keep framework-native file structure.
- Where syntax supports metadata comments, add Dosya Yolu, Amac, Modul - FileType, Version, Aciklama, Bagimli Oldugu Katman. Unknown absolute paths use REPO_ROOT/relative-path. Never insert comments into strict JSON, binary or incompatible formats.
- Keep secrets/private data out of code, logs and artifacts. Use configuration and secret stores, avoid magic strings.
- Prove build, unit, contract/integration and smoke/UI results separately. Do not report tests as PASS unless actually run. Artifacts are not releases.
- Never run public or fork PR code on a personal self-hosted runner. Use trusted sandbox CI and least-privilege permissions.
- Central repo is private and may be inaccessible to external contributors; this file is a usable standalone contract. An inaccessible document is never claimed read. New standards land here only after a separate project PR.

No application code, active workflow or product release is modified by this document.
