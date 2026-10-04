// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/IPackageEngine.cs
// 📌 Amac: Package engine adapterlarinin staging, apply, repair ve rollback davranisini tanimlar
// 📌 Modul - Port CSharp
// Version: 0.3.0
// Aciklama: Velopack gibi motorlari Core Application katmanindan ayiran paketleme portudur
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Plans;

namespace TurkuazInstaller.Contracts.Packages;

public interface IPackageEngine
{
    Task StageAsync(InstallPlan plan, string verifiedArtifactPath, string stagingDirectory, CancellationToken cancellationToken);
    Task ApplyAsync(InstallPlan plan, string stagingDirectory, CancellationToken cancellationToken);
    Task RepairAsync(RepairPlan plan, string verifiedArtifactPath, CancellationToken cancellationToken);
    Task RollbackAsync(RollbackPlan plan, string verifiedArtifactPath, CancellationToken cancellationToken);
}
