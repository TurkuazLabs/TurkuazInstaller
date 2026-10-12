// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/InstallerInstallMode.cs
// 📌 Amac: Paylasilan kurulum sihirbazinin hizli ve ozellestirilmis modlarini tipli olarak tanimlar
// 📌 Modul - Contract CSharp
// Version: 1.0.0
// Aciklama: Kurulum modunu View ve Service arasinda magic string olmadan tasir
//
// Bagimli Oldugu Katman: Controller | Service | View

namespace TurkuazInstaller.Contracts.Operations;

public enum InstallerInstallMode
{
    Quick = 0,
    Custom = 1
}
