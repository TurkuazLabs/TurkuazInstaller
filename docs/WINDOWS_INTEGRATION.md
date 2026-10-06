# 📄 Dosya Yolu: /docs/WINDOWS_INTEGRATION.md
# 📌 Amac: TurkuazInstaller signed Windows shortcut ve URL protocol integration action guvenlik modelini tanimlamak
# 📌 Modul - Markdown
# Version: 1.1.0
# Aciklama: HKCU-only protocol ownership, shortcut hash receipt, install-root executable containment ve uninstall cleanup davranislarini sabitler
# Bagimli Oldugu Katman: Domain | Service | Repo | Tool | Config

# Safe Windows Integration Actions

Windows integration actionlari yalniz signed installer manifest icinden gelir.

Desteklenen action tipleri:

- Desktop shortcut
- Start Menu shortcut
- per-user URL protocol registration

Machine-wide registry veya elevation kullanilmaz.

## Manifest

Ornek:

```yaml
install:
  windows:
    shortcuts:
      - id: main
        name: Example App
        location: start_menu
        executable: ExampleApp.exe
      - id: desktop
        name: Example App
        location: desktop
        executable: ExampleApp.exe
    protocols:
      - scheme: example-app
        executable: ExampleApp.exe
```

Manifest raw byte'lari detached CMS/PKCS#7 trust pipeline'ini gecmeden integration action parse edilmez.

## Executable Siniri

Shortcut/protocol target executable:

- install rootuna gore relative olmalidir
- .exe uzantili olmalidir
- install root disina cikamaz
- path traversal kullanamaz
- reparse-point zincirinden gecemez
- package apply tamamlandiktan sonra gercekten mevcut olmalidir

Integration action package artifact apply'dan once calismaz.

## Shortcut Ownership

Shortcut receipt:

`%LOCALAPPDATA%/TurkuazInstaller/integrations/{package_id}.json`

Her shortcut icin receipt:

- action id
- full .lnk path
- SHA-256

tutar.

Var olan shortcut:

- receipt yoksa overwrite edilmez
- receipt path eslesmiyorsa overwrite edilmez
- mevcut .lnk SHA-256 receipt ile eslesmiyorsa overwrite edilmez

Cleanup yalniz mevcut .lnk SHA-256 receipt hash ile birebir eslesiyorsa siler.

Receipt path ayrica package-owned lokasyon allow-listinden gecmelidir:

- Desktop shortcut icin kullanici Desktop klasorunun dogrudan alti
- Start Menu shortcut icin package-scoped TurkuazInstaller/{package_id} klasorunun dogrudan alti

Bozuk veya elle degistirilmis receipt keyfi filesystem path temizligi baslatamaz.

Kullanici veya baska uygulama shortcut'i degistirdiyse TurkuazInstaller dosyayi silmez.

Start Menu shortcutlari package-scoped klasorde tutulur:

`%APPDATA%/Microsoft/Windows/Start Menu/Programs/TurkuazInstaller/{package_id}/`

Desktop shortcut kullanici Desktop klasorunde olusturulur.

## URL Protocol Ownership

Protocol registration yalniz:

`HKCU\Software\Classes\{scheme}`

altinda yapilir.

Machine-wide HKLM registration yoktur.

Ownership marker:

`TurkuazInstallerOwner = {package_id}`

Var olan scheme:

- marker yoksa foreign kabul edilir
- marker farkli package id ise foreign kabul edilir
- foreign scheme overwrite edilmez

Protocol claim scheme bazli cross-process mutex ile serialize edilir.

Receipt icindeki protocol scheme degeri Domain scheme validatorundan tekrar gecmeden registry cleanup yolu uretilmez.

Command formati:

`"C:\...\App.exe" "%1"`

Shell invocation kullanilmaz.

## Reconcile

Install/update/repair/rollback sonrasinda desired integration policy receipt ile reconcile edilir.

Yeni action:

- safe ownership kontrolu
- create/apply
- working receipt persist

Stale action:

- yalniz package-owned ise cleanup

Integration apply state commit sonrasinda calisir.

Integration reconcile hata verirse operation failed olarak raporlanir; working receipt daha sonra safe retry icin sahiplik bilgisini korur.

## Uninstall

Package engine uninstall basarili olduktan sonra owned integration cleanup denenir.

Cleanup failure:

- structured warning loglanir
- package state delete devam eder
- integration receipt korunur

Receipt korunmasinin nedeni daha sonra cleanup retry yapabilmektir.

Cleanup basarili olursa receipt silinir.

## Guvenlik Siniri

TurkuazInstaller:

- foreign shortcut overwrite etmez
- modified owned shortcut silmez
- foreign protocol overwrite etmez
- foreign protocol silmez
- install root disindaki executable'i target yapmaz
- reparse-point target kabul etmez
- machine-wide registry yazmaz
- shell command interpretation kullanmaz
