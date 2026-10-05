// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/PackageInstallMode.cs
// 📌 Amac: Manifest install payload modunu typed domain degeri olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Stable v1 icin yalniz tam paket kurulumunu destekler; delta manifest iddiasi yapmaz
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Releases;

public enum PackageInstallMode
{
    Full = 0
}
