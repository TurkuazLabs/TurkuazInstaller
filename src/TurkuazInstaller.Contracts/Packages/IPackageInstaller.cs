// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/IPackageInstaller.cs
// 📌 Amac: Paket motoru adapterlari icin install portunu tanimlar
// 📌 Modul - Contract CSharp
// Version: 0.1.0
// Aciklama: Velopack, MSIX veya MSI motorlarinin Core'a baglanacagi sinirdir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Packages;

public interface IPackageInstaller
{
    Task InstallAsync(
        ReleaseDescriptor release,
        string localPackagePath,
        CancellationToken cancellationToken);
}
