// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/InstallerInstallScope.cs
// 📌 Amac: Her urun icin ayni kurulum kapsam kimligini tip guvenli sekilde tanimlar
// 📌 Modul - Contract CSharp
// Version: 1.0.0
// Aciklama: Yol prefixinden bagimsiz per-user/per-machine kurulum sozlesmesi
//
// Bagimli Oldugu Katman: Service | Repo | Tool | View | Config

namespace TurkuazInstaller.Contracts.Operations;

public enum InstallerInstallScope
{
    PerUser = 0,
    PerMachine = 1
}
