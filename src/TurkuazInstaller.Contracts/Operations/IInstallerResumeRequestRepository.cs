// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/IInstallerResumeRequestRepository.cs
// 📌 Amac: Reboot resume request snapshotlarini saklayan repository portunu tanimlar
// 📌 Modul - Repo CSharp
// Version: 1.0.0
// Aciklama: Service katmanini JSON veya baska resume storage implementasyonundan ayirir
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Contracts.Operations;

public interface IInstallerResumeRequestRepository
{
    Task<InstallerResumeRequest?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        InstallerResumeRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        PackageId packageId,
        CancellationToken cancellationToken);
}
