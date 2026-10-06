// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/BootstrapSelfUpdateRelease.cs
// 📌 Amac: Bootstrap self-update discovery sonucundaki immutable release kimligini tasir
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: Version, HTTPS artifact URI, GitHub SHA-256 digest ve size bilgisini Service/Tool sinirinda toplar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Bootstrap;

public sealed record BootstrapSelfUpdateRelease
{
    public BootstrapSelfUpdateRelease(
        SemanticVersion version,
        Uri artifactUri,
        ArtifactDigest artifactDigest,
        long sizeBytes)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(artifactUri);
        ArgumentNullException.ThrowIfNull(artifactDigest);

        if (
            !artifactUri.IsAbsoluteUri ||
            !string.Equals(
                artifactUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Bootstrap self-update artifact URI must use HTTPS.",
                nameof(artifactUri));
        }

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes));
        }

        Version = version;
        ArtifactUri = artifactUri;
        ArtifactDigest = artifactDigest;
        SizeBytes = sizeBytes;
    }

    public SemanticVersion Version { get; }

    public Uri ArtifactUri { get; }

    public ArtifactDigest ArtifactDigest { get; }

    public long SizeBytes { get; }
}
