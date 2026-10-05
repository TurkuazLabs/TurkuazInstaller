// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Artifacts/Sha256ArtifactVerifier.cs
// 📌 Amac: Artifact size ve SHA-256 integrity dogrulamasini yapan Tool adapterini uygular
// 📌 Modul - Tool CSharp
// Version: 0.7.0
// Aciklama: Apply oncesi artifact boyutu ve digest degerini manifest beklentisiyle karsilastirir
//
// Bagimli Oldugu Katman: Tool

using System.Security.Cryptography;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Infrastructure.Artifacts;

public sealed class Sha256ArtifactVerifier : IArtifactVerifier
{
    private const string MissingMessage = "Artifact file does not exist.";
    private const string SizeMismatchMessage = "Artifact size does not match the manifest.";
    private const string HashMismatchMessage = "Artifact SHA-256 does not match the manifest.";

    public async Task<VerificationResult> VerifyAsync(
        string artifactPath,
        ArtifactDescriptor expectedArtifact,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        ArgumentNullException.ThrowIfNull(expectedArtifact);

        if (!File.Exists(artifactPath))
        {
            return VerificationResult.Failed(
                VerificationFailure.PathRejected,
                MissingMessage);
        }

        var fileInfo = new FileInfo(artifactPath);

        if (fileInfo.Length != expectedArtifact.SizeBytes)
        {
            return VerificationResult.Failed(
                VerificationFailure.SizeMismatch,
                SizeMismatchMessage);
        }

        await using var stream = new FileStream(
            artifactPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = await SHA256
            .HashDataAsync(
                stream,
                cancellationToken)
            .ConfigureAwait(false);

        var digest = Convert
            .ToHexString(hash)
            .ToLowerInvariant();

        if (!string.Equals(
                digest,
                expectedArtifact.Digest.Sha256,
                StringComparison.Ordinal))
        {
            return VerificationResult.Failed(
                VerificationFailure.HashMismatch,
                HashMismatchMessage);
        }

        return VerificationResult.Passed();
    }
}
