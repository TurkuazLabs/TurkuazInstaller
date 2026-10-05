# 📄 Dosya Yolu: /docs/ARCHITECTURE.md
# 📌 Amac: TurkuazInstaller Community katmanlarini ve bagimlilik yonunu tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Stable v1 Domain, Application, Port, Adapter, ViewModel ve View sinirlarini contract-first tanimlar
# Bagimli Oldugu Katman: Service | Repo | Tool | View | Config

# Architecture

Zorunlu bagimlilik yonu:

```text
View
  -> Controller
      -> Presentation Service
          -> Application Use Case
              -> Port
                  <- Adapter
              -> Domain
```

## Domain

Teknoloji bagimsiz kurallar:

- PackageId
- SemanticVersion
- ReleaseChannel
- ArtifactDigest
- ArtifactSignatureDescriptor
- PackageInstallPolicy
- PackageRollbackPolicy
- Prerequisite
- InstallPlan
- UpdatePlan
- RepairPlan
- RollbackPlan
- UninstallPlan
- VerificationResult

Domain WinUI, GitHub, Gitea, Velopack veya HTTP client bilmez.

## Application

Stable v1 use-case siniri:

- CheckForUpdate
- InstallPackage
- UpdatePackage
- RepairPackage
- RollbackPackage
- UninstallPackage

Application manifest policy verisini uygular:

- prerequisites
- preserve paths
- rollback support
- required signature

Provider-specific HTTP veya process detayi Application katmaninda tutulmaz.

## Port

Public contractlar:

- ReleaseProvider
- ArtifactDownloader
- ArtifactVerifier
- ArtifactSignatureVerifier
- PackageEngine
- InstallStateRepository
- SystemPrerequisiteProbe
- ProcessRunner

Package Engine apply oncesinde typed PackageStage zorunlu tutar.

Uninstall artifact staging gerektirmez; kurulu state uzerinden Package Engine'e gider ve basarili engine sonucundan sonra state silinir.

## Adapter

Community adapterlari:

- GitHub Release Provider
- Gitea Release Provider
- Generic HTTPS Provider
- File Provider
- Velopack Package Engine
- System Process Runner
- Windows Authenticode Verifier
- Windows System Prerequisite Probe

Velopack adapteri merkezi installer senaryosunda Setup.exe ve Update.exe CLI kontratini Tool katmaninda kapsuller.

## UI

WinUI 3 yalniz View ve platform presentation tarafindadir.

Controller is kurali tasimaz.

UI tarafinda:

- Kur
- Guncelle
- Onar
- Geri Al
- Kaldir
- Iptal
- Tekrar Dene

operasyonlari ayni Application workflow katmanina gider.

CLI sonraki minor surumde ayni Application use-case katmanini kullanacak ayri bir View/Controller olacaktir.

## Pro Siniri

Community hicbir zaman TurkuazSoft/TurkuazInstaller-Pro kaynak koduna bagimli olmaz.

Pro, Community public contract ve package katmanini tuketebilir.
