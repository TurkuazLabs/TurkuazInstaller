// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Operations/InstallerOperationPhase.cs
// 📌 Amac: Installer operation journal checkpoint asamalarini typed olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.2.0
// Aciklama: Crash/reboot recovery ve diagnostics icin operasyonun hangi checkpointte kaldigini kalici olarak ifade eder
//
// Bagimli Oldugu Katman: Service | Repo | Tool

namespace TurkuazInstaller.Domain.Operations;

public enum InstallerOperationPhase
{
    Started = 0,
    ValidatingPrerequisites = 1,
    Downloading = 2,
    Verifying = 3,
    Staging = 4,
    Applying = 5,
    SavingState = 6,
    RemovingState = 7,
    Completed = 8,
    Cancelled = 9,
    Failed = 10,
    AwaitingReboot = 11
}
