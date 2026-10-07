// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/PackageRelease.cs
// 📌 Amac: Provider tarafindan bulunan paket release, artifact ve install/rollback policy bilgisini typed domain modelinde birlestirir
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Full artifact yaninda optional exact-base delta optimizasyon metadata'sini da provider bagimsiz release modelinde tasir
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Domain.Releases;

public sealed record PackageRelease
{
    public PackageRelease(
        PackageId packageId,
        SemanticVersion version,
        ReleaseChannel channel,
        ArtifactDescriptor artifact)
        : this(
            packageId,
            version,
            channel,
            artifact,
            PackageInstallPolicy.LegacyDefault,
            PackageRollbackPolicy.Disabled)
    {
    }

    public PackageRelease(
        PackageId packageId,
        SemanticVersion version,
        ReleaseChannel channel,
        ArtifactDescriptor artifact,
        PackageInstallPolicy install,
        PackageRollbackPolicy rollback,
        PackageDeltaArtifact? deltaArtifact = null)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(install);
        ArgumentNullException.ThrowIfNull(rollback);

        if (
            deltaArtifact is not null &&
            deltaArtifact.FromVersion >= version)
        {
            throw new ArgumentException(
                "Delta artifact base version must be older than the target release.",
                nameof(deltaArtifact));
        }

        PackageId = packageId;
        Version = version;
        Channel = channel;
        Artifact = artifact;
        Install = install;
        Rollback = rollback;
        DeltaArtifact = deltaArtifact;
    }

    public PackageId PackageId { get; }

    public SemanticVersion Version { get; }

    public ReleaseChannel Channel { get; }

    public ArtifactDescriptor Artifact { get; }

    public PackageInstallPolicy Install { get; }

    public PackageRollbackPolicy Rollback { get; }

    public PackageDeltaArtifact? DeltaArtifact { get; }
}
