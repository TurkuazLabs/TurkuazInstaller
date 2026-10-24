// Dosya Yolu: tests/TurkuazInstaller.Infrastructure.Tests/PinnedThemeTrustPolicyRepositoryTests.cs
// Amac: Harici TPK'nin kendi trusted publisher tanimlayamamasini kanitlamak
// Modul - CSharp xUnit Tests
// Version: 0.1.0-qa
// Aciklama: In-memory immutable pins, missing pin, wrong product ve gercek CMS
// Bagimli Oldugu Katman: Infrastructure | Security | Contracts

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Branding;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class PinnedThemeTrustPolicyRepositoryTests
{
    private const string Product = "levelupgt.ragnar-classic-preview";

    [Fact]
    public async Task EmptyPinsNeverAcceptEvenAValidCmsSigner()
    {
        using var certificate = CreateTestCertificate();
        var signature = Sign(certificate, out var content);
        var verifier = new CmsManifestSignatureVerifier(
            new PinnedThemeTrustPolicyRepository(
                Array.Empty<KeyValuePair<string, ManifestTrustPolicy>>()));
        var result = await verifier.VerifyAsync(
            content, signature, PackageId.Parse(Product), CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task WrongPinnedCertificateIsRejected()
    {
        using var signer = CreateTestCertificate();
        using var other = CreateTestCertificate();
        var signature = Sign(signer, out var content);
        var verifier = new CmsManifestSignatureVerifier(Repository(
            new Dictionary<string, ManifestTrustPolicy> {
                [Product] = Policy(other)
            }));
        var result = await verifier.VerifyAsync(
            content, signature, PackageId.Parse(Product), CancellationToken.None);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task PinMustMatchAndCannotBeReplacedByCallerMutation()
    {
        using var signer = CreateTestCertificate();
        using var attacker = CreateTestCertificate();
        var provided = new Dictionary<string, ManifestTrustPolicy> {
            [Product] = Policy(signer)
        };
        var snapshot = Repository(provided);
        // The app compiled a snapshot; mutable config cannot swap trust later.
        provided[Product] = Policy(attacker);
        provided["another.product"] = Policy(attacker);

        var signature = Sign(signer, out var content);
        var verifier = new CmsManifestSignatureVerifier(snapshot);
        var valid = await verifier.VerifyAsync(
            content, signature, PackageId.Parse(Product), CancellationToken.None);
        Assert.True(valid.IsValid);
        Assert.Null(await snapshot.GetAsync(
            PackageId.Parse("another.product"), CancellationToken.None));

        var attackerSignature = Sign(attacker, out var attackerContent);
        var replacement = await verifier.VerifyAsync(
            attackerContent, attackerSignature, PackageId.Parse(Product),
            CancellationToken.None);
        Assert.False(replacement.IsValid);
    }

    [Fact]
    public void RejectsDuplicateAndInvalidProductTrustEntries()
    {
        using var cert = CreateTestCertificate();
        var pin = Policy(cert);
        var repeated = new[] {
            new KeyValuePair<string, ManifestTrustPolicy>(Product, pin),
            new KeyValuePair<string, ManifestTrustPolicy>(Product, pin)
        };
        Assert.Throws<ArgumentException>(() => Repository(repeated));
        Assert.Throws<FormatException>(() => Repository(
            new Dictionary<string, ManifestTrustPolicy> { ["../evil"] = pin }));
    }

    private static PinnedThemeTrustPolicyRepository Repository(
        IEnumerable<KeyValuePair<string, ManifestTrustPolicy>> policies) => new(policies);

    private static ManifestTrustPolicy Policy(X509Certificate2 cert) =>
        new(cert.Subject, cert.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant());

    private static X509Certificate2 CreateTestCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Theme Test Only", key,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-3), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static byte[] Sign(X509Certificate2 certificate, out byte[] content)
    {
        content = Encoding.UTF8.GetBytes("immutable signed theme manifest fixture");
        var signed = new SignedCms(new ContentInfo(content), detached: true);
        signed.ComputeSignature(new CmsSigner(
            SubjectIdentifierType.IssuerAndSerialNumber, certificate) {
                IncludeOption = X509IncludeOption.EndCertOnly
            });
        return signed.Encode();
    }
}
