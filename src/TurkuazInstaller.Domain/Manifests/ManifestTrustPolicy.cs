// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Manifests/ManifestTrustPolicy.cs
// 📌 Amac: Bir package manifestini imzalamasina izin verilen publisher kimligini immutable Domain modeli olarak tasir
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Publisher subject ve zorunlu sertifika SHA-256 pinini normalize edip fail-closed dogrular
//
// Bagimli Oldugu Katman: Service | Repo | Tool

namespace TurkuazInstaller.Domain.Manifests;

public sealed record ManifestTrustPolicy
{
    private const int Sha256HexLength = 64;

    public ManifestTrustPolicy(
        string publisherSubject,
        string certificateSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            publisherSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            certificateSha256);

        var normalizedCertificateSha256 =
            certificateSha256
                .Trim()
                .ToLowerInvariant();

        if (
            normalizedCertificateSha256.Length !=
                Sha256HexLength ||
            !normalizedCertificateSha256.All(
                Uri.IsHexDigit))
        {
            throw new FormatException(
                "Manifest trust certificate SHA-256 must contain exactly 64 hexadecimal characters.");
        }

        PublisherSubject =
            publisherSubject.Trim();

        CertificateSha256 =
            normalizedCertificateSha256;
    }

    public string PublisherSubject { get; }

    public string CertificateSha256 { get; }
}
