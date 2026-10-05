// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Prerequisites/PrerequisiteInstallDisposition.cs
// 📌 Amac: Prerequisite installer process sonucunu typed ve platformdan bagimsiz olarak siniflandirir
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Basari, reboot gerekli ve reboot baslatildi sonuclarini Service katmanina tasir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Prerequisites;

public enum PrerequisiteInstallDisposition
{
    Completed = 0,
    RebootRequired = 1,
    RebootInitiated = 2
}
