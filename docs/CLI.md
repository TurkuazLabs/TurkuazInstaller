# 📄 Dosya Yolu: /docs/CLI.md
# 📌 Amac: TurkuazInstaller CLI ve silent-mode kullanim/guvenlik contractini tanimlamak
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Komutlar, zorunlu optionlar, exit code'lar, silent output ve reboot-resume davranisini sabitler
# Bagimli Oldugu Katman: Controller | Service | Repo | Tool | Config

# CLI / Silent Mode

CLI executable combined Windows dagitiminda su konumdadir:

`cli/TurkuazInstaller.Cli.exe`

CLI interaktif prompt kullanmaz.

Eksik veya gecersiz option fail-closed olarak `InvalidInvocation` exit code ile sonlanir.

## Komutlar

Desteklenen public operasyonlar:

- install
- update
- repair
- rollback
- uninstall

Ornek install:

```text
TurkuazInstaller.Cli.exe install --package example-app --manifest https://example.invalid/installer-manifest.yml --channel stable --target C:\Apps\Example
```

Silent install:

```text
TurkuazInstaller.Cli.exe install --package example-app --manifest https://example.invalid/installer-manifest.yml --silent
```

Rollback:

```text
TurkuazInstaller.Cli.exe rollback --package example-app --manifest current.yml --rollback-manifest previous.yml --silent
```

Uninstall manifest gerektirmez:

```text
TurkuazInstaller.Cli.exe uninstall --package example-app --silent
```

## Option Contract

- `--package`: tum public operasyonlarda zorunlu
- `--manifest`: uninstall disinda zorunlu
- `--rollback-manifest`: rollback icin zorunlu
- `--channel stable|beta`: optional, default stable
- `--target`: install icin optional; yoksa signed manifest default target kullanilir
- `--silent`: stdout/stderr progress ve hata metnini bastirir

Ayni option tekrar edilemez.

Bilinmeyen option reddedilir.

CLI shell expression, response file veya environment variable tabanli implicit command execution kullanmaz.

## Exit Code Contract

- 0: Success
- 2: InvalidInvocation
- 3: RebootRequired
- 4: OperationFailed
- 5: Cancelled

Automation sistemleri metin parse etmek yerine exit code kullanmalidir.

## Trust Zinciri

CLI, GUI ile ayni runtime guvenlik zincirini kullanir:

1. detached CMS/PKCS#7 manifest trust
2. package/version/channel validation
3. artifact size + SHA-256
4. manifest isterse Authenticode publisher/certificate pin
5. prerequisite detector registry
6. prerequisite installer SHA-256 + Authenticode
7. package-scoped operation lock
8. operation journal
9. persisted reboot resume request
10. Velopack package engine

CLI icin guvenlik bypass yolu yoktur.

## Silent Reboot Resume

Silent CLI prerequisite installer 3010 veya 1641 donerse:

1. resume request ve journal checkpoint kalici tutulur
2. HKCU RunOnce kaydi ayni CLI executable'ini hedefler
3. komut yalniz package id ve `--silent` tasir
4. reboot sonrasi manifest yeniden signed trust zincirinden gecirilir
5. expected version ve artifact SHA-256 tekrar pinlenir
6. pending prerequisite yeniden probe edilir
7. ancak basariliysa package apply devam eder

Silent CLI reboot sonrasinda WinUI acmaz.

## Storage

CLI ve WinUI ayni package-scoped storage alanlarini kullanir:

`%LOCALAPPDATA%\TurkuazInstaller`

Bu nedenle GUI ve CLI ayni package icin paralel mutation baslatamaz.

Pending reboot operasyonu varsa normal CLI/GUI mutation reddedilir.
