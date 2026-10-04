# 📄 Dosya Yolu: /docs/ARCHITECTURE.md
# 📌 Amac: TurkuazInstaller Community katmanlarini ve bagimlilik yonunu tanimlamak
# 📌 Modul - Markdown
# Version: 0.5.0
# Aciklama: Domain, Application, Port, Adapter, ViewModel ve View sinirlarini contract-first tanimlar
# Bagimli Oldugu Katman: Service | Repo | Tool | View | Config

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
- UpdatePlan
- RepairPlan
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

Is akislarini koordine eder; provider-specific HTTP veya process detayi tutmaz.

## Port

Public contractlar:

- ReleaseProvider
- ArtifactDownloader
- ArtifactVerifier
- PackageEngine
- InstallStateRepository
- SystemPrerequisiteProbe
- ProcessRunner

Package Engine apply oncesinde typed `PackageStage` zorunlu tutar.

## Adapter

Community adapterlari:

- GitHub Release Provider
- Gitea Release Provider
- Generic HTTPS Provider
- File Provider
- Velopack Package Engine
- System Process Runner

Velopack adapteri merkezi installer senaryosunda Setup.exe ve Update.exe CLI kontratini Tool katmaninda kapsuller.

## UI

GUI ve CLI ayni Application use-case katmanini kullanir.

WinUI 3 yalniz View ve ViewModel tarafindadir.

## Pro Siniri

Community hicbir zaman `TurkuazSoft/TurkuazInstaller-Pro` kaynak koduna bagimli olmaz.

Pro, Community public contract ve package katmanini tuketebilir.
