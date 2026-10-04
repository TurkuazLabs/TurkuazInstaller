// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Plans/UpdatePlan.cs
// 📌 Amac: Mevcut surumden hedef install planina update gecisini typed olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Update hedefinin mevcut surumden yeni olmasini domain seviyesinde dogrular
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Domain.Plans;

public sealed record UpdatePlan
{
    public UpdatePlan(SemanticVersion currentVersion, InstallPlan target)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Release.Version <= currentVersion)
        {
            throw new ArgumentException("Update target version must be newer than current version.", nameof(target));
        }

        CurrentVersion = currentVersion;
        Target = target;
    }

    public SemanticVersion CurrentVersion { get; }
    public InstallPlan Target { get; }
}
