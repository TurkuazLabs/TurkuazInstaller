// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerProgressStage.cs
// 📌 Amac: Install, update, repair ve rollback workflow progress asamalarini typed olarak tanimlar
// 📌 Modul - Service CSharp
// Version: 0.7.0
// Aciklama: UI progress ve log katmanlarina magic string olmadan operasyon asamasi aktarir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Operations;

public enum InstallerProgressStage
{
    Downloading = 0,
    Verifying = 1,
    Staging = 2,
    Applying = 3,
    SavingState = 4,
    Completed = 5
}
