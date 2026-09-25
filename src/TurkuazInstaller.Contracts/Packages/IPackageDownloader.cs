// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/IPackageDownloader.cs
// 📌 Amac: Paket indirme adapterlari icin port kontratini tanimlar
// 📌 Modul - Contract CSharp
// Version: 0.1.0
// Aciklama: Transport implementasyonlarini Application katmanindan ayirir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Packages;

public interface IPackageDownloader
{
    Task<string> DownloadAsync(
        PackageDescriptor package,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
