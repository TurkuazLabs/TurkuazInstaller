// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Updates/IVersionUpdatePolicyRepository.cs
// 📌 Amac: Package/channel bazli version skip/pinning policy resolve repository portunu tanimlar
// 📌 Modul - Repo CSharp
// Version: 1.0.0
// Aciklama: Application katmanini LocalAppData JSON policy storage implementasyonundan ayirir
//
// Bagimli Oldugu Katman: Repo | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Updates;

public interface IVersionUpdatePolicyRepository
{
    Task<VersionUpdatePolicy?> GetAsync(
        PackageId packageId,
        ReleaseChannel channel,
        CancellationToken cancellationToken);
}
