// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/PackageStage.cs
// 📌 Amac: Dogrulanmis artifactin atomik staging sonucunu typed kontrat olarak tasir
// 📌 Modul - Port CSharp
// Version: 0.5.0
// Aciklama: Paket kimligi, surum, artifact turu ve staged dosya yolunu apply islemlerine guvenli sekilde aktarir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Packages;

public sealed record PackageStage
{
    public PackageStage(
        PackageId packageId,
        SemanticVersion version,
        PackageArtifactKind artifactKind,
        string artifactPath)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);

        PackageId = packageId;
        Version = version;
        ArtifactKind = artifactKind;
        ArtifactPath = artifactPath.Trim();
    }

    public PackageId PackageId { get; }

    public SemanticVersion Version { get; }

    public PackageArtifactKind ArtifactKind { get; }

    public string ArtifactPath { get; }
}
