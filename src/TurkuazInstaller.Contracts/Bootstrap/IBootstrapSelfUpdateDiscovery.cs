// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/IBootstrapSelfUpdateDiscovery.cs
// 📌 Amac: Bootstrap icin daha yeni trusted release metadata'sini kesfeden Tool portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Mevcut semantic versiondan daha yeni stable release varsa typed update release dondurur
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Bootstrap;

public interface IBootstrapSelfUpdateDiscovery
{
    Task<BootstrapSelfUpdateRelease?> GetLatestAsync(
        SemanticVersion currentVersion,
        CancellationToken cancellationToken);
}
