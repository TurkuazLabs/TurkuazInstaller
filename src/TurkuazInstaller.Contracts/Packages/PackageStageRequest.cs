// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/PackageStageRequest.cs
// 📌 Amac: Package Engine staging isteginde release, secilen artifact ve optional kurulu hedef yolunu typed olarak tasir
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Full ve delta artifact staging akisini Application katmanindan Tool adapterine magic parametre olmadan aktarir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Packages;

public sealed record PackageStageRequest
{
    public PackageStageRequest(
        PackageRelease release,
        ArtifactDescriptor artifact,
        string? installedTargetPath = null)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(artifact);

        Release = release;
        Artifact = artifact;
        InstalledTargetPath =
            string.IsNullOrWhiteSpace(installedTargetPath)
                ? null
                : installedTargetPath.Trim();
    }

    public PackageRelease Release { get; }

    public ArtifactDescriptor Artifact { get; }

    public string? InstalledTargetPath { get; }
}
