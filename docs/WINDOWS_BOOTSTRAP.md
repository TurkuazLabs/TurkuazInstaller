# 📄 Dosya Yolu: /docs/WINDOWS_BOOTSTRAP.md
# 📌 Amac: TurkuazInstaller Windows NativeAOT bootstrap startup, app launch ve self-update mimarisini dokumante etmek
# 📌 Modul - Markdown
# Version: 1.2.1
# Aciklama: Combined distribution, prerequisite, reboot resume forwarding, trusted self-update discovery/download ve least-privilege process sinirlarini tanimlar
# Bagimli Oldugu Katman: Service | Tool | Config

# Windows Bootstrap

TurkuazInstaller kullanicinin calistirdigi tek giris executable dosyasi olarak NativeAOT bootstrap kullanir.

Stable v1 combined distribution:

```text
TurkuazInstaller.Bootstrapper.exe
app/
  TurkuazInstaller.WinUI.exe
  ...
```

## Normal Startup

Normal akis:

1. staged self-update cleanup varsa best-effort temizlenir
2. Windows prerequisite kontrolu yapilir
3. desteklenmeyen OS/build/architecture typed exit code ile reddedilir
4. signed current bootstrap self-update icin uygun mu kontrol edilir
5. GitHub latest stable release uzerinden daha yeni bootstrap kesfedilir
6. replacement size + GitHub SHA-256 digest ile dogrulanir
7. replacement Authenticode signer current bootstrap signer subject + certificate SHA-256 ile eslestirilir
8. update varsa two-process handoff baslatilir ve app argumentlari korunur
9. update yoksa app/TurkuazInstaller.WinUI.exe yolu distribution root containment ile cozulur
10. WinUI process unelevated ve shell kullanmadan baslatilir
11. kullanici islemlerini WinUI/Application workflow devam ettirir

Bootstrap kendisine verilen normal application argumentlarini shell kullanmadan WinUI processine aktarir.

Prerequisite reboot resume icin internal protocol:

```text
TurkuazInstaller.Bootstrapper.exe --resume-package <package-id>
```

Bu argument RunOnce tarafindan yalniz package kimligini tasir. Manifest source, target path veya install command registry icinde tasinmaz.

WinUI bu package id ile persisted resume request ve AwaitingReboot journal kaydini tekrar dogrular.

Stable v1 release target win-x64 olarak sabitlenmistir.

ARM64 native release sonraki minor faza birakilmistir; desteklenmeyen architecture bootstrap tarafinda acikca reddedilir.

## Self-update Discovery

Automatic discovery GitHub latest stable release endpointini kullanir.

Yalniz current assembly SemVer degerinden daha yeni release kabul edilir.

Required asset:

TurkuazInstaller.Bootstrapper.exe

Asset icin GitHub size ve sha256 digest zorunludur. Download URL yalniz github.com HTTPS olabilir.

Unsigned veya Windows trust kontrolunden gecmeyen current bootstrap auto-update yapmaz.

Detay: docs/BOOTSTRAP_SELF_UPDATE.md

## Self-update Start

Automatic discovery/download veya explicit internal replacement icin internal protocol:

```text
TurkuazInstaller.Bootstrapper.exe --self-update-replacement <verified-replacement.exe> [app arguments]
```

Bootstrap:

1. current executable yolunu Tool portundan alir
2. SelfUpdateStartRequest olusturur
3. WindowsSelfUpdateHandoff.BeginAsync cagirir
4. replacement process complete-self-update modunda baslatilir
5. current bootstrap kapanir

Bu internal arguman son kullanici update kaynagi degildir.

Explicit replacement yolu da artik current bootstrap ile ayni trusted Authenticode publisher subject ve certificate SHA-256 kimligini tasimadan handoff baslatamaz.

## Self-update Completion

Replacement process once target mevcut bootstrap ile source replacement signer kimligini tekrar dogrular.

Ardindan:

1. parent process exit bekler
2. target bootstrap dosyasini retry ile degistirir
3. yeni target bootstrap'i resume argumentlariyla baslatir
4. yeni target process staged source cleanup yapar
5. normal prerequisite + WinUI launch akisina devam eder

## Process Boundary

Bootstrap ve WinUI varsayilan olarak admin baslatilmaz.

Application launch UseShellExecute=false kullanir.

Administrator hakki gereken gelecekteki explicit operasyonlar IElevatedProcessRunner sinirindan gecmelidir.
