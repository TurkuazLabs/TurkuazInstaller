// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Operations/InstallerOperationType.cs
// 📌 Amac: Installer mutasyon operasyonlarini Domain seviyesinde typed olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Journal, log, GUI ve CLI tarafinda install/update/repair/rollback/uninstall magic stringlerini engeller
//
// Bagimli Oldugu Katman: Service | Repo | Tool | View

namespace TurkuazInstaller.Domain.Operations;

public enum InstallerOperationType
{
    Install = 0,
    Update = 1,
    Repair = 2,
    Rollback = 3,
    Uninstall = 4
}
