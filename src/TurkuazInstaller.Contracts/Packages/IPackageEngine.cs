// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/IPackageEngine.cs
// 📌 Amac: Package engine adapterlarinin staging, apply, repair ve rollback davranisini tanimlar
// 📌 Modul - Port CSharp
// Version: 0.5.0
// Aciklama: Dogrulanmis artifacti typed stage haline getirir ve tum mutasyonlari staged artifact uzerinden calistirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Packages;

public interface IPackageEngine
{
    Task<PackageStage> StageAsync(
        PackageRelease release,
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
}
