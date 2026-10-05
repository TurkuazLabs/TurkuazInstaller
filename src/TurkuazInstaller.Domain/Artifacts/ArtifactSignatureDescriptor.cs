// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Artifacts/ArtifactSignatureDescriptor.cs
// 📌 Amac: Artifact signature algoritmasi ile beklenen publisher kimligini typed domain policy olarak tasir
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Authenticode trust zincirine ek olarak publisher subject ve optional certificate SHA-256 pinning uygular
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Artifacts;

public sealed record ArtifactSignatureDescriptor
{
    public ArtifactSignatureDescriptor(
        ArtifactSignatureAlgorithm algorithm,
        string publisherSubject,
        string? certificateSha256 = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            publisherSubject);

        Algorithm = algorithm;
        PublisherSubject = publisherSubject.Trim();
        CertificateSha256 =
            NormalizeOptionalSha256(
                certificateSha256);
    }

    public ArtifactSignatureAlgorithm Algorithm { get; }

    public string PublisherSubject { get; }

    public string? CertificateSha256 { get; }

    private static string? NormalizeOptionalSha256(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized =
            value
                .Trim()
                .Replace(
                    ":",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    " ",
                    string.Empty,
                    StringComparison.Ordinal)
                .ToLowerInvariant();

        if (
            normalized.Length != 64 ||
            normalized.Any(
                character =>
                    !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "Certificate SHA-256 pin must contain exactly 64 hexadecimal characters.",
                nameof(value));
        }

        return normalized;
    }
}
