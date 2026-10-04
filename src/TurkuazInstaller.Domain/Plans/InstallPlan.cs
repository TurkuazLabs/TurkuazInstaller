// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Plans/InstallPlan.cs
// 📌 Amac: Bir paketin guvenli install apply planini immutable domain modeli olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Release, hedef, prerequisite ve preserve path verilerini tek planda toplar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Prerequisites;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Domain.Plans;

public sealed record InstallPlan
{
    public InstallPlan(PackageRelease release, string targetPath, IReadOnlyList<Prerequisite> prerequisites, IReadOnlyList<string> preservePaths)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(prerequisites);
        ArgumentNullException.ThrowIfNull(preservePaths);

        Release = release;
        TargetPath = targetPath.Trim();
        Prerequisites = prerequisites;
        PreservePaths = preservePaths;
    }

    public PackageRelease Release { get; }
    public string TargetPath { get; }
    public IReadOnlyList<Prerequisite> Prerequisites { get; }
    public IReadOnlyList<string> PreservePaths { get; }
}
