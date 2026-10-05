// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/State/IInstallStateRepository.cs
// 📌 Amac: Kurulu paket state storage okuma, yazma ve silme islemleri icin repository portunu tanimlar
// 📌 Modul - Repo CSharp
// Version: 1.0.0
// Aciklama: Application katmanini registry, JSON veya baska storage implementasyonlarindan ayirir
//
// Bagimli Oldugu Katman: Repo

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.State;

namespace TurkuazInstaller.Contracts.State;

public interface IInstallStateRepository
{
    Task<InstalledPackageState?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken);

    Task SaveAsync(
        InstalledPackageState state,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        PackageId packageId,
        CancellationToken cancellationToken);
}
