// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Plans/RollbackPlan.cs
// 📌 Amac: Guvenli rollback icin mevcut ve onceki release bilgisini typed olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Rollback hedefinin mevcut surumden eski olmasini zorunlu tutar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Domain.Plans;

public sealed record RollbackPlan
{
    public RollbackPlan(PackageRelease currentRelease, PackageRelease previousRelease, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(currentRelease);
        ArgumentNullException.ThrowIfNull(previousRelease);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        if (previousRelease.Version >= currentRelease.Version)
        {
            throw new ArgumentException("Rollback target must be older than current version.", nameof(previousRelease));
        }

        CurrentRelease = currentRelease;
        PreviousRelease = previousRelease;
        TargetPath = targetPath.Trim();
    }

    public PackageRelease CurrentRelease { get; }
    public PackageRelease PreviousRelease { get; }
    public string TargetPath { get; }
}
