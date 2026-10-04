# 📄 Dosya Yolu: /docs/WINDOWS_BOOTSTRAP.md
# 📌 Amac: TurkuazInstaller Windows NativeAOT bootstrap mimarisini, self-update ve UAC sinirlarini dokumante etmek
# 📌 Modul - Markdown
# Version: 0.6.0
# Aciklama: Native runtime, prerequisite, iki-process handoff ve least-privilege elevation modelini tanimlar
# Bagimli Oldugu Katman: Service | Tool | Config

# Windows Bootstrap

TurkuazInstaller bootstrap executable .NET 10 NativeAOT olarak publish edilir.

NativeAOT sonucu self-contained native Windows executable uretilir ve hedef makinede ayrica .NET runtime kurulumu zorunlu olmaz.

## Platform Baseline

Bootstrap prerequisite policy:

- Windows zorunlu
- minimum Windows 10 1809
- minimum build 17763
- x64 ve Arm64 OS mimarileri kabul edilir

Bu baseline sonraki WinUI 3 ve Windows App SDK fazi ile uyumludur.

## Native Publish

Windows CI win-x64 NativeAOT publish yapar:

```text
dotnet publish ... --runtime win-x64 --self-contained true
```

Bootstrap projectte `PublishAot=true` sabittir.

Linux Core CI Windows native publish yapmaz. Windows bootstrap ayri Windows kalite kapisina sahiptir.

## Prerequisite Boundary

`BootstrapPrerequisiteService` platform API kullanmaz.

Runtime bilgisi:

```text
BootstrapPrerequisiteService
    -> IBootstrapEnvironmentProbe
        -> WindowsBootstrapEnvironmentProbe
```

Bu sayede prerequisite kararlari unit test ile Windows disinda da dogrulanabilir.

## Self-update Handoff

Bootstrap kendi calisan executable dosyasini dogrudan overwrite etmeye calismaz.

Akis:

1. dogrulanmis yeni bootstrap staged executable olarak hazirlanir
2. current bootstrap replacement executable'i handoff modunda baslatir
3. current process kapanir
4. replacement process parent exit bekler
5. target executable retry policy ile degistirilir
6. target executable yeniden baslatilir
7. yeni target process staged source icin best-effort cleanup yapar

Self-update handoff otomatik elevation yapmaz.

Source ve target executable dosya adlari ayni olmak zorundadir.

Complete handoff source path degeri calisan replacement process ile birebir eslesmelidir.

## Elevation Boundary

Normal process calistirma unelevated kalir.

Administrator hakki gereken ilerideki operasyonlar yalniz:

```text
IElevatedProcessRunner
    -> WindowsElevatedProcessRunner
```

uzerinden calistirilir.

Windows adapter `runas` verb'i ile UAC consent ister.

Kullanici UAC dialogunu iptal ederse bu durum exception yerine typed `ElevationStatus.Cancelled` olarak dondurulur.

Tum bootstrap veya UI processi varsayilan olarak admin baslatilmaz.

## Sonraki Faz

v0.7.0 WinUI 3 fazi bootstrap prerequisite kontrolunden sonra GUI processini baslatacak ve install/update/repair/rollback use-case durumlarini gosterecek.
