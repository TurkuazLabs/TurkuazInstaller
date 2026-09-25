// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/ReleaseChannel.cs
// 📌 Amac: Update release kanalini domain seviyesinde tanimlar
// 📌 Modul - Domain CSharp
// Version: 0.1.0
// Aciklama: Kanallari magic string kullanmadan temsil eder
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Releases;

public enum ReleaseChannel
{
    Stable = 0,
    Beta = 1,
    Dev = 2,
    Canary = 3
}
