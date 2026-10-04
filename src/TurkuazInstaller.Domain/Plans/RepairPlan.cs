// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Plans/RepairPlan.cs
// 📌 Amac: Kurulu paketin integrity repair planini typed domain modeli olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Repair icin beklenen release ve install hedefini immutable olarak tasir
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Domain.Plans;

public sealed record RepairPlan
{
    public RepairPlan(PackageRelease release, string targetPath)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        Release = release;
        TargetPath = targetPath.Trim();
    }

    public PackageRelease Release { get; }
    public string TargetPath { get; }
}
