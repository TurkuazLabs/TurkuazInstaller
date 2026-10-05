// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/CmsManifestSignatureVerifier.cs
// 📌 Amac: CMS/PKCS#7 detached manifest imzasini package bazli publisher ve sertifika pini ile dogrular
// 📌 Modul - Tool CSharp
// Version: 1.1.1
// Aciklama: Manifest parserdan once kriptografik signature, tek signer, certificate validity, subject ve SHA-256 pin kontrollerini fail-closed uygular
//
// Bagimli Oldugu Katman: Tool | Repo | Service

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Infrastructure.Manifests;

public sealed class CmsManifestSignatureVerifier
    : IManifestSignatureVerifier
{
    private const int ExpectedSignerCount = 1;

    private readonly IManifestTrustPolicyRepository
        _trustPolicyRepository;

    public CmsManifestSignatureVerifier(
        IManifestTrustPolicyRepository trustPolicyRepository)
    {
        ArgumentNullException.ThrowIfNull(
            trustPolicyRepository);

        _trustPolicyRepository =
            trustPolicyRepository;
    }

    public async Task<VerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> manifestContent,
        ReadOnlyMemory<byte> detachedSignature,
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);

        cancellationToken.ThrowIfCancellationRequested();

        if (manifestContent.IsEmpty)
        {
            return VerificationResult.Failed(
                VerificationFailure.SignatureInvalid,
                "Manifest content is empty.");
        }

        if (detachedSignature.IsEmpty)
        {
            return VerificationResult.Failed(
                VerificationFailure.SignatureInvalid,
                "Detached manifest signature is missing.");
        }

        var trustPolicy =
            await _trustPolicyRepository
                .GetAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (trustPolicy is null)
        {
            return VerificationResult.Failed(
                VerificationFailure.SignatureInvalid,
                string.Concat(
                    "No manifest trust policy is configured for package '",
                    packageId.Value,
                    "'."));
        }

        try
        {
            var signedCms =
                new SignedCms(
                    new ContentInfo(
                        manifestContent.ToArray()),
                    detached: true);

            signedCms.Decode(
                detachedSignature.ToArray());

            if (
                signedCms.SignerInfos.Count !=
                ExpectedSignerCount)
            {
                return VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    "Detached manifest signature must contain exactly one signer.");
            }

            var signer =
                signedCms.SignerInfos[0];

            signer.CheckSignature(
                verifySignatureOnly: true);

            var certificate =
                signer.Certificate;

            if (certificate is null)
            {
                return VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    "Detached manifest signature does not contain a signer certificate.");
            }

            var now =
                DateTime.UtcNow;

            if (
                now <
                    certificate
                        .NotBefore
                        .ToUniversalTime() ||
                now >
                    certificate
                        .NotAfter
                        .ToUniversalTime())
            {
                return VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    "Detached manifest signer certificate is outside its validity period.");
            }

            if (
                !string.Equals(
                    certificate.Subject,
                    trustPolicy.PublisherSubject,
                    StringComparison.OrdinalIgnoreCase))
            {
                return VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    "Detached manifest publisher subject does not match the configured trust policy.");
            }

            var certificateSha256 =
                certificate
                    .GetCertHashString(
                        HashAlgorithmName.SHA256)
                    .ToLowerInvariant();

            if (
                !string.Equals(
                    certificateSha256,
                    trustPolicy.CertificateSha256,
                    StringComparison.Ordinal))
            {
                return VerificationResult.Failed(
                    VerificationFailure.SignatureInvalid,
                    "Detached manifest certificate SHA-256 pin does not match the configured trust policy.");
            }

            return VerificationResult.Passed();
        }
        catch (CryptographicException exception)
        {
            return VerificationResult.Failed(
                VerificationFailure.SignatureInvalid,
                string.Concat(
                    "Detached manifest signature verification failed: ",
                    exception.Message));
        }
    }
}
