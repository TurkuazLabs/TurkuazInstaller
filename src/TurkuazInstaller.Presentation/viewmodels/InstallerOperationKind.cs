// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/viewmodels/InstallerOperationKind.cs
// 📌 Amac: Masaustu arayuzundeki installer operasyon secimini typed enum olarak tanimlar
// 📌 Modul - ViewModel CSharp
// Version: 1.0.0
// Aciklama: Install, update, repair, rollback ve uninstall butonlarini magic string kullanmadan Service katmanina aktarir
//
// Bagimli Oldugu Katman: View

namespace TurkuazInstaller.Presentation.ViewModels;

public enum InstallerOperationKind
{
    Install = 0,
    Update = 1,
    Repair = 2,
    Rollback = 3,
    Uninstall = 4
}
