// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/PackageArtifactKind.cs
// 📌 Amac: Package Engine tarafindan desteklenen Velopack artifact turlerini typed olarak tanimlar
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Setup, full nupkg ve optional delta nupkg artifactlarini magic string kullanmadan ayirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.Packages;

public enum PackageArtifactKind
{
    VelopackSetup = 0,
    VelopackFullPackage = 1,
    VelopackDeltaPackage = 2
}
