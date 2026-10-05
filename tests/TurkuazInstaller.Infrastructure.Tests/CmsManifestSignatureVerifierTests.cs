// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/CmsManifestSignatureVerifierTests.cs
// 📌 Amac: CMS/PKCS#7 detached manifest verification, publisher pinning ve sertifika SHA-256 trust anchor davranisini dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.1
// Aciklama: Gercek ephemeral RSA sertifika ve SignedCms ile valid, tampered, wrong-pin ve missing-policy senaryolarini test eder
//
// Bagimli Oldugu Katman: Tool | Repo | Service

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class CmsManifestSignatureVerifierTests
{
    [Fact]
    public async Task VerifyAsync_AcceptsPinnedDetachedSignature()
    {
        using var certificate =
            CreateCertificate();

        var manifest =
            Encoding.UTF8.GetBytes(
                "schema_version: 1");

        var signature =
            Sign(
                manifest,
                certificate);

        var verifier =
            CreateVerifier(
                certificate);

        var result =
            await verifier.VerifyAsync(
                manifest,
                signature,
                PackageId.Parse(
                    ProviderTestData.PackageId),
                CancellationToken.None);

        Assert.True(
            result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_RejectsTamperedManifest()
    {
        using var certificate =
            CreateCertificate();

        var originalManifest =
            Encoding.UTF8.GetBytes(
                "schema_version: 1");

        var tamperedManifest =
            Encoding.UTF8.GetBytes(
                "schema_version: 2");

        var signature =
            Sign(
                originalManifest,
                certificate);

        var verifier =
            CreateVerifier(
                certificate);

        var result =
            await verifier.VerifyAsync(
                tamperedManifest,
                signature,
                PackageId.Parse(
                    ProviderTestData.PackageId),
                CancellationToken.None);

        Assert.False(
            result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_RejectsCertificatePinMismatch()
    {
        using var certificate =
            CreateCertificate();

        var manifest =
            Encoding.UTF8.GetBytes(
                "schema_version: 1");

        var signature =
            Sign(
                manifest,
                certificate);

        var verifier =
            new CmsManifestSignatureVerifier(
                new FakeManifestTrustPolicyRepository(
                    new ManifestTrustPolicy(
                        certificate.Subject,
                        new string(
                            '0',
                            64))));

        var result =
            await verifier.VerifyAsync(
                manifest,
                signature,
                PackageId.Parse(
                    ProviderTestData.PackageId),
                CancellationToken.None);

        Assert.False(
            result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_RejectsMissingTrustPolicy()
    {
        using var certificate =
            CreateCertificate();

        var manifest =
            Encoding.UTF8.GetBytes(
                "schema_version: 1");

        var signature =
            Sign(
                manifest,
                certificate);

        var verifier =
            new CmsManifestSignatureVerifier(
                new FakeManifestTrustPolicyRepository(
                    null));

        var result =
            await verifier.VerifyAsync(
                manifest,
                signature,
                PackageId.Parse(
                    ProviderTestData.PackageId),
                CancellationToken.None);

        Assert.False(
            result.IsValid);
    }

    private static CmsManifestSignatureVerifier CreateVerifier(
        X509Certificate2 certificate)
    {
        var certificateSha256 =
            certificate
                .GetCertHashString(
                    HashAlgorithmName.SHA256)
                .ToLowerInvariant();

        return new CmsManifestSignatureVerifier(
            new FakeManifestTrustPolicyRepository(
                new ManifestTrustPolicy(
                    certificate.Subject,
                    certificateSha256)));
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa =
            RSA.Create(
                2048);

        var request =
            new CertificateRequest(
                "CN=Example Software",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(
                -5),
            DateTimeOffset.UtcNow.AddDays(
                7));
    }

    private static byte[] Sign(
        byte[] manifest,
        X509Certificate2 certificate)
    {
        var signedCms =
            new SignedCms(
                new ContentInfo(
                    manifest),
                detached: true);

        var signer =
            new CmsSigner(
                SubjectIdentifierType.IssuerAndSerialNumber,
                certificate)
            {
                IncludeOption =
                    X509IncludeOption.EndCertOnly
            };

        signedCms.ComputeSignature(
            signer,
            silent: true);

        return signedCms.Encode();
    }
}
