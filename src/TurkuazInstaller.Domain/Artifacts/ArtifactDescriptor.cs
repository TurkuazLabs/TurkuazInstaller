// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Artifacts/ArtifactDescriptor.cs
// 📌 Amac: Release artifact adres, hash ve boyut metadata'sini typed modelde toplar
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Remote plain HTTP reddi ve pozitif boyut kurallarini domain seviyesinde uygular
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Artifacts;

public sealed record ArtifactDescriptor
{
    public ArtifactDescriptor(Uri uri, ArtifactDigest digest, long sizeBytes)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(digest);

        if (!uri.IsAbsoluteUri) throw new ArgumentException("Artifact URI must be absolute.", nameof(uri));
        if (uri.Scheme is not ("https" or "file")) throw new ArgumentException("Artifact URI must use https or file scheme.", nameof(uri));
        if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        Uri = uri;
        Digest = digest;
        SizeBytes = sizeBytes;
    }

    public Uri Uri { get; }
    public ArtifactDigest Digest { get; }
    public long SizeBytes { get; }
}
