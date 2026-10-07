// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/IPackageEngine.cs
// 📌 Amac: Package engine adapterlarinin staging, apply, repair, rollback ve uninstall davranisini tanimlar
// 📌 Modul - Port CSharp
// Version: 1.1.0
// Aciklama: Secilmis full/delta artifact staging requestini typed tasir ve installer mutasyonlarini package engine portu uzerinden calistirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Packages;

public interface IPackageEngine
{
    Task<PackageStage> StageAsync(
        PackageStageRequest request,
        string verifiedArtifactPath,
        string stagingDirectory,
        CancellationToken cancellationToken);

    Task ApplyAsync(
        InstallPlan plan,
        PackageStage stage,
        CancellationToken cancellationToken);

    Task RepairAsync(
        RepairPlan plan,
        PackageStage stage,
        CancellationToken cancellationToken);

    Task RollbackAsync(
        RollbackPlan plan,
        PackageStage stage,
        CancellationToken cancellationToken);

    Task UninstallAsync(
        UninstallPlan plan,
        CancellationToken cancellationToken);
}
