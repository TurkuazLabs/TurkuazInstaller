// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/State/InstalledPackageState.cs
// 📌 Amac: Kurulu paket state bilgisini storage bagimsiz typed model olarak tasir
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Update kararlari icin package, version, channel ve target bilgisini tutar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Domain.State;

public sealed record InstalledPackageState(
    PackageId PackageId,
    SemanticVersion Version,
    ReleaseChannel Channel,
    string TargetPath);
