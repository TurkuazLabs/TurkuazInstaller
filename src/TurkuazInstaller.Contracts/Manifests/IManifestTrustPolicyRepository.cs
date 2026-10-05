// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Manifests/IManifestTrustPolicyRepository.cs
// 📌 Amac: Package bazli manifest publisher trust policy bilgisini Infrastructure storage detayindan ayirir
// 📌 Modul - Repo Port CSharp
// Version: 1.1.0
// Aciklama: Manifest signer subject ve sertifika SHA-256 pinini package id ile resolve eden repository kontratidir
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Contracts.Manifests;

public interface IManifestTrustPolicyRepository
{
    Task<ManifestTrustPolicy?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken);
}
