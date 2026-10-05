# 📄 Dosya Yolu: /docs/WINUI_DESKTOP.md
# 📌 Amac: TurkuazInstaller WinUI 3 desktop mimarisi, runtime composition ve recovery UX kararlarini dokumante etmek
# 📌 Modul - Markdown
# Version: 1.1.0
# Aciklama: Manifest policy, Windows verification, reboot resume startup, uninstall ve gercek workflow baglantisini tanimlar
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

## Runtime Composition

Desktop composition:

- manifest -> HTTPS veya local file provider
- artifact -> HTTPS veya file downloader
- integrity -> size + SHA-256
- signature -> Windows WinVerifyTrust Authenticode verifier
- prerequisites -> Windows system prerequisite probe
- package engine -> Velopack
- state -> atomic JSON repository
- operation journal -> atomic JSON repository
- reboot resume request -> atomic JSON repository
- reboot relaunch -> HKCU RunOnce Tool adapter
- workflow -> InstallerWorkflowService

## Operasyonlar

UI gercek olarak:

- install
- update
- repair
- rollback
- uninstall

operasyonlarini calistirir.

Uninstall manifest gerektirmez ve installed state uzerinden calisir.

## Reboot Resume Startup

MainWindow ilk activation eventinde startup argumentlarini yalniz bir kez Controller -> Presentation Service akisina aktarir.

Internal --resume-package requesti:

1. package id olarak parse edilir
2. Runtime Service resume request ve journal kaydini yukler
3. AwaitingReboot checkpointini dogrular
4. signed manifesti yeniden cozer
5. expected version ve package artifact SHA-256 kimligini dogrular
6. pending prerequisite'i yeniden probe eder
7. ancak sonra normal package workflow devam eder

Resume startup parse veya trust kontrolu basarisizsa View hata state'ine gecer ve package mutation baslamaz.

3010/1641 ilk yakalandiginda UI normal failure yerine reboot required statusu gosterir.

## Manifest-driven Target

Install target ekranda override edilebilir.

Ekran bos birakilirsa manifest install.target degeri Environment variable expansion sonrasinda kullanilir.

Update, repair, rollback ve uninstall mevcut installed state icindeki gercek target path ile devam eder.

## Progress

UI su state'leri gosterir:

- ready
- preparing
- downloading
- verifying
- staging
- applying
- uninstalling
- saving state
- removing state
- completed
- cancelled
- reboot required
- failed

Failure sonrasi Retry aktif olur.

Calisan operasyon Cancel ile CancellationToken uzerinden iptal edilebilir.

## State

Installed package state kullanici LocalApplicationData dizini altinda paket bazli JSON dosyasinda tutulur.

State save atomic temp-file replace ile tamamlanir.

Uninstall state delete yalniz Package Engine basarili olduktan sonra yapilir.
