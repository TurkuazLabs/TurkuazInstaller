# 📄 Dosya Yolu: /docs/WINDOWS_BOOTSTRAP.md
# 📌 Amac: TurkuazInstaller Windows NativeAOT bootstrap startup, app launch ve self-update mimarisini dokumante etmek
# 📌 Modul - Markdown
# Version: 1.0.0
# Aciklama: Combined distribution, prerequisite, explicit self-update begin/complete ve least-privilege process sinirlarini tanimlar
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
4. app/TurkuazInstaller.WinUI.exe yolu distribution root containment ile cozulur
5. WinUI process unelevated ve shell kullanmadan baslatilir
6. kullanici islemlerini WinUI/Application workflow devam ettirir

Stable v1 release target win-x64 olarak sabitlenmistir.

ARM64 native release sonraki minor faza birakilmistir; desteklenmeyen architecture bootstrap tarafinda acikca reddedilir.

## Self-update Start

Daha once SHA-256/signature verification katmanindan gecmis replacement bootstrap icin internal protocol:

```text
TurkuazInstaller.Bootstrapper.exe --self-update-replacement <verified-replacement.exe> [app arguments]
```

Bootstrap:

1. current executable yolunu Tool portundan alir
2. SelfUpdateStartRequest olusturur
3. WindowsSelfUpdateHandoff.BeginAsync cagirir
4. replacement process complete-self-update modunda baslatilir
5. current bootstrap kapanir

Bu internal arguman son kullanici update kaynagi degildir; replacement artifacti bu asamaya gelmeden once dogrulanmis olmalidir.

## Self-update Completion

Replacement process:

1. parent process exit bekler
2. target bootstrap dosyasini retry ile degistirir
3. yeni target bootstrap'i resume argumentlariyla baslatir
4. yeni target process staged source cleanup yapar
5. normal prerequisite + WinUI launch akisina devam eder

## Process Boundary

Bootstrap ve WinUI varsayilan olarak admin baslatilmaz.

Application launch UseShellExecute=false kullanir.

Administrator hakki gereken gelecekteki explicit operasyonlar IElevatedProcessRunner sinirindan gecmelidir.
