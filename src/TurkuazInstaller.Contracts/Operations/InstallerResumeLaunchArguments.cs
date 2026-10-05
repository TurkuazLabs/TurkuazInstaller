// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/InstallerResumeLaunchArguments.cs
// 📌 Amac: Bootstrap ile WinUI arasindaki internal reboot resume command-line protokolunu sabitler
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Resume package option degerini magic string olmadan ortak kullanima acar
//
// Bagimli Oldugu Katman: Service | Tool | Controller

namespace TurkuazInstaller.Contracts.Operations;

public static class InstallerResumeLaunchArguments
{
    public const string PackageOption =
        "--resume-package";
}
