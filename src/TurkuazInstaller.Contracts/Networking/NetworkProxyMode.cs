// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Networking/NetworkProxyMode.cs
// 📌 Amac: TurkuazInstaller HTTP proxy calisma modlarini typed contract olarak tanimlar
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: System proxy, direct connection ve explicit custom proxy modlarini magic string disina tasir
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Contracts.Networking;

public enum NetworkProxyMode
{
    System = 0,
    Direct = 1,
    Custom = 2
}
