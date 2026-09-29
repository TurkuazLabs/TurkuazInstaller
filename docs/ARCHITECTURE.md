# 📄 Dosya Yolu: /docs/ARCHITECTURE.md
# 📌 Amac: TurkuazInstaller Community katmanlarini ve bagimlilik yonunu tanimlamak
# 📌 Modul - Markdown
# Version: 0.2.0
# Aciklama: Domain, Application, Port, Adapter, ViewModel ve View sinirlarini contract-first tanimlar
# Bagimli Oldugu Katman: Domain | Application | Port | Adapter | ViewModel | View

# Architecture

Zorunlu bagimlilik yonu:

```text
View
  |
  v
ViewModel
  |
  v
Application Use Case
  |
  +--> Port <--- Adapter
  |
  v
Domain
```

## Domain

Teknoloji bagimsiz kurallar:

- PackageId
- SemanticVersion
- ReleaseChannel
- ArtifactDigest
- InstallPlan
- RollbackPlan
- VerificationResult

Domain WinUI, GitHub, Gitea, Velopack veya HTTP client bilmez.

## Application

Use-case siniri:

- CheckForUpdate
- InstallPackage
- UpdatePackage
- RepairPackage
- RollbackPackage
- UninstallPackage

Is akislarini koordine eder; provider-specific HTTP detayi tutmaz.

## Port

Public contractlar:

- ReleaseProvider
- ArtifactDownloader
- ArtifactVerifier
- PackageEngine
- InstallStateRepository
- SystemPrerequisiteProbe

## Adapter

Community adapter hedefleri:

- GitHub Release Provider
- Gitea Release Provider
- Generic HTTPS Provider
- File Provider
- Velopack Package Engine

## UI

GUI ve CLI ayni Application use-case katmanini kullanir.

WinUI 3 yalniz View/ViewModel tarafindadir.

## Pro Siniri

Community hicbir zaman `TurkuazSoft/TurkuazInstaller-Pro` kaynak koduna bagimli olmaz.

Pro, Community public contract/package katmanini tuketebilir.
