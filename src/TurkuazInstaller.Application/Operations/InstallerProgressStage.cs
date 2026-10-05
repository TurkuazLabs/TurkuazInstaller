// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerProgressStage.cs
// 📌 Amac: Installer workflow progress asamalarini typed olarak tanimlar
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Install, update, repair, rollback ve uninstall UI progress olaylarini magic string olmadan aktarir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Operations;

public enum InstallerProgressStage
{
    Downloading = 0,
    Verifying = 1,
    Staging = 2,
    Applying = 3,
    Uninstalling = 4,
    SavingState = 5,
    RemovingState = 6,
    Completed = 7
}
