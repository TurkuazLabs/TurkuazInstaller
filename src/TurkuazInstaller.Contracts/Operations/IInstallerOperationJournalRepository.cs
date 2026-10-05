// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/IInstallerOperationJournalRepository.cs
// 📌 Amac: Installer operation journal snapshotlarini saklayan repository portunu tanimlar
// 📌 Modul - Repo CSharp
// Version: 1.1.0
// Aciklama: Application katmanini JSON veya baska crash-recovery journal storage implementasyonundan ayirir
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Contracts.Operations;

public interface IInstallerOperationJournalRepository
{
    Task<InstallerOperationJournalEntry?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        InstallerOperationJournalEntry entry,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        PackageId packageId,
        CancellationToken cancellationToken);
}
