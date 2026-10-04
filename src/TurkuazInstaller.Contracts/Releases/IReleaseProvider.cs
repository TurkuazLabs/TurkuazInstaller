// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Releases/IReleaseProvider.cs
// 📌 Amac: GitHub, Gitea, HTTPS ve file adapterlari icin ortak release provider portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 0.3.0
// Aciklama: Application katmaninin provider teknolojisini bilmeden latest release sorgulamasini saglar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Releases;

public interface IReleaseProvider
{
    Task<PackageRelease?> GetLatestReleaseAsync(PackageId packageId, ReleaseChannel channel, CancellationToken cancellationToken);
}
