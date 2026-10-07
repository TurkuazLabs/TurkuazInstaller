// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/PackageDeltaArtifact.cs
// 📌 Amac: Hedef release icin optional Velopack delta artifactini ve taban surumunu typed olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Delta optimizasyonunu tam paket artifactindan ayirir ve exact taban surumu kontratini Domain katmaninda tasir
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Artifacts;

namespace TurkuazInstaller.Domain.Releases;

public sealed record PackageDeltaArtifact
{
    public PackageDeltaArtifact(
        SemanticVersion fromVersion,
        ArtifactDescriptor artifact)
    {
        ArgumentNullException.ThrowIfNull(fromVersion);
        ArgumentNullException.ThrowIfNull(artifact);

        FromVersion = fromVersion;
        Artifact = artifact;
    }

    public SemanticVersion FromVersion { get; }

    public ArtifactDescriptor Artifact { get; }
}
