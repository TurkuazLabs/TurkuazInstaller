// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsBootstrapSelfUpdateTrustVerifier.cs
// 📌 Amac: Bootstrap self-update replacementinin Authenticode signer kimligini calisan trusted bootstrap ile eslestirir
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Current executable signer subject/certificate SHA-256 degerini trust anchor yapar ve replacementta ayni trusted signer'i zorunlu tutar
//
// Bagimli Oldugu Katman: Tool | Service

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Bootstrap;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsBootstrapSelfUpdateTrustVerifier
    : IBootstrapSelfUpdateTrustVerifier
{
    private readonly IArtifactSignatureVerifier _signatureVerifier;

    public WindowsBootstrapSelfUpdateTrustVerifier()
        : this(
            new WindowsAuthenticodeArtifactSignatureVerifier())
    {
    }

    public WindowsBootstrapSelfUpdateTrustVerifier(
        IArtifactSignatureVerifier signatureVerifier)
    {
        _signatureVerifier = signatureVerifier;
    }

    public async Task<bool> CanSelfUpdateAsync(
        string currentExecutablePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            currentExecutablePath);

        cancellationToken.ThrowIfCancellationRequested();

        if (
            !OperatingSystem.IsWindows() ||
            !File.Exists(
                currentExecutablePath))
        {
            return false;
        }

        ArtifactSignatureDescriptor expectedSignature;

        try
        {
            expectedSignature =
                ReadCurrentSignerPolicy(
                    currentExecutablePath);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var result =
            await _signatureVerifier
                .VerifyAsync(
                    currentExecutablePath,
                    expectedSignature,
                    cancellationToken)
                .ConfigureAwait(false);

        return result.IsValid;
    }

    public async Task<VerificationResult> VerifyReplacementAsync(
        string currentExecutablePath,
        string replacementExecutablePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            currentExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            replacementExecutablePath);

        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return VerificationResult.Failed(
                VerificationFailure.SignatureInvalid,
                "Bootstrap self-update Authenticode verification requires Windows.");
        }

        if (
            !File.Exists(
                currentExecutablePath) ||
            !File.Exists(
                replacementExecutablePath))
        {
            return VerificationResult.Failed(
                VerificationFailure.PathRejected,
                "Bootstrap self-update current or replacement executable does not exist.");
        }

        ArtifactSignatureDescriptor expectedSignature;

        try
        {
            expectedSignature =
                ReadCurrentSignerPolicy(
                    currentExecutablePath);
        }
        catch (CryptographicException exception)
        {
            return VerificationResult.Failed(
                VerificationFailure.SignatureInvalid,
                string.Concat(
                    "Current bootstrap signer identity could not be read: ",
                    exception.Message));
        }

        var currentResult =
            await _signatureVerifier
                .VerifyAsync(
                    currentExecutablePath,
                    expectedSignature,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!currentResult.IsValid)
        {
            return VerificationResult.Failed(
                VerificationFailure.SignatureInvalid,
                "Current bootstrap Authenticode trust validation failed.");
        }

        return await _signatureVerifier
            .VerifyAsync(
                replacementExecutablePath,
                expectedSignature,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static ArtifactSignatureDescriptor
        ReadCurrentSignerPolicy(
            string currentExecutablePath)
    {
#pragma warning disable SYSLIB0057
        using var certificate =
            X509Certificate.CreateFromSignedFile(
                Path.GetFullPath(
                    currentExecutablePath));
#pragma warning restore SYSLIB0057

        return new ArtifactSignatureDescriptor(
            ArtifactSignatureAlgorithm.Authenticode,
            certificate.Subject,
            certificate
                .GetCertHashString(
                    HashAlgorithmName.SHA256)
                .ToLowerInvariant());
    }
}
