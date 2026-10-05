# 📄 Dosya Yolu: /docs/WINUI_DESKTOP.md
# 📌 Amac: TurkuazInstaller WinUI 3 desktop mimarisi, runtime composition ve recovery UX kararlarini dokumante etmek
# 📌 Modul - Markdown
# Version: 0.7.0
# Aciklama: Presentation siniri, self-contained deployment, gercek workflow baglantisi ve UI durum modelini tanimlar
# Bagimli Oldugu Katman: Controller | Service | Repo | Tool | View | Language | Config

# WinUI Desktop

TurkuazInstaller masaustu arayuzu Windows App SDK 2.5.1 stable ve WinUI 3 kullanir.

## Katmanlar

```text
View
  -> Controller
      -> Presentation Service
          -> Runtime Service
              -> Application Workflow
                  -> Port
                      -> Repo / Tool
```

View code-behind is kurali tasimaz.

Controller yalniz kullanici requestini Presentation Service katmanina aktarir.

Progress, validation, cancel ve retry state'i Presentation Service ve ViewModel katmaninda tutulur.

## Deployment

WinUI uygulamasi unpackaged ve self-contained olarak publish edilir.

Project ayarlari:

- `WindowsPackageType=None`
- `WindowsAppSDKSelfContained=true`
- `SelfContained=true`
- Windows App SDK 2.5.1 stable

Bu model Windows App SDK runtime dosyalarini publish outputuna dahil eder.

## Gercek Operasyonlar

UI butonlari mock operasyon calistirmaz.

Runtime composition:

- manifest source -> HTTPS veya local file provider
- artifact -> HTTPS veya file downloader
- verification -> size + SHA-256
- package apply -> Velopack Package Engine
- state -> atomic JSON repository
- workflow -> Application InstallerWorkflowService

Install:

1. release resolve
2. artifact download
3. SHA-256 verification
4. atomic staging
5. Velopack apply
6. installed state save

Update ayni pipeline'i daha yeni release zorunlulugu ile calistirir.

Repair kurulu surumle birebir eslesen release artifactini tekrar uygular.

Rollback current manifest ile onceki surum manifestini ayri kaynaklardan alir ve onceki release artifactini uygular.

## Error Recovery

UI su state'leri gosterir:

- ready
- preparing
- downloading
- verifying
- staging
- applying
- saving state
- completed
- cancelled
- failed

Failure sonrasi Retry aktif olur.

Calisan operasyon Cancel ile CancellationToken uzerinden iptal edilebilir.

## Manifest Source

v0.7 desktop ekraninda kullanici dogrudan:

- HTTPS `installer-manifest.yml` URL
- local manifest dosya yolu
- `file:` URI

girebilir.

GitHub ve Gitea adapterlari Core'da mevcut kalir. Desktop UI icin dogrudan manifest URL girisi en basit ortak runtime siniridir.

## State

Installed package state kullanici LocalApplicationData dizini altinda paket bazli JSON dosyasinda tutulur.

State write atomic temp-file replace ile tamamlanir.

## Sonraki Adim

v1.0.0 fazinda signed release pipeline, recovery integration testleri, security review ve son kullanici dokumani tamamlanacak.
