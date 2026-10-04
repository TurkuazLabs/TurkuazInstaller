// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/ReleaseChannel.cs
// 📌 Amac: Community release kanallarini typed enum olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Contract ile sabitlenen stable ve beta kanallarini temsil eder
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Releases;

public enum ReleaseChannel
{
    Stable = 0,
    Beta = 1
}
