// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/SignedFileManifestIntegrationTests.cs
// 📌 Amac: Yerel manifest providerindan gercek detached CMS ve harici YAML trust pini ile signed manifest kabul/reddini dogrular
// 📌 Modul - Test CSharp
// Version: 0.1.0
// Aciklama: Test sirasinda ephemeral RSA sertifikasi ile valid, tampered, yanlis pin, farkli signer, eksik signature ve eksik policy kontrolleri
// Bagimli Oldugu Katman: Service | Repo | Tool | Config

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.File;
using TurkuazInstaller.Infrastructure.Repositories;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class SignedFileManifestIntegrationTests
{
    private const string FixtureVersion = "1.0.0";
    private const string FixtureSubject = "CN=Example Software";
    private const string StableManifestName = "installer-manifest.yml";
    private const string BetaManifestName = "beta-manifest.yml";
    private const string TrustFileName = "manifest-trust.yml";
    private const string TrustTemplateName = "manifest-trust-ci.template.yml";
    private const string TrustTemplateDirectory = "config";
    private const string CertificatePinToken = "__CERTIFICATE_SHA256__";

    [Fact]
    public async Task GetLatestReleaseAsync_LoadsRealSignedManifestWithExternalCertificatePin()
    {
        using var fixture = await Fixture.CreateAsync();

        var release = await fixture.CreateProvider().GetLatestReleaseAsync(
            PackageId.Parse(ProviderTestData.PackageId),
            ReleaseChannel.Stable,
            CancellationToken.None);

        Assert.NotNull(release);
        Assert.Equal(FixtureVersion, release.Version.ToString());
        Assert.Equal(ProviderTestData.PackageId, release.PackageId.Value);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsValidYamlWhoseSignedBytesChanged()
    {
        using var fixture = await Fixture.CreateAsync();

        var changedManifest = ProviderTestData.Manifest(
            ReleaseChannel.Stable,
            "1.0.1");
        await File.WriteAllTextAsync(fixture.ManifestPath, changedManifest);

        await AssertRejectedAsync(fixture);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsWrongExternalCertificatePin()
    {
        using var fixture = await Fixture.CreateAsync();

        await fixture.WriteTrustStoreAsync(new string('0', 64));

        await AssertRejectedAsync(fixture);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsDifferentSignerEvenWithSameSubject()
    {
        using var fixture = await Fixture.CreateAsync();
        using var replacement = Fixture.CreateCertificate();

        var manifest = await File.ReadAllBytesAsync(fixture.ManifestPath);
        var differentSignature = Fixture.Sign(manifest, replacement);
        await File.WriteAllBytesAsync(fixture.SignaturePath, differentSignature);

        await AssertRejectedAsync(fixture);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsMissingDetachedSignature()
    {
        using var fixture = await Fixture.CreateAsync();

        File.Delete(fixture.SignaturePath);

        await AssertRejectedAsync(fixture);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsMissingIndependentTrustStore()
    {
        using var fixture = await Fixture.CreateAsync();

        File.Delete(fixture.TrustPath);

        await AssertRejectedAsync(fixture);
    }

    private static async Task AssertRejectedAsync(Fixture fixture)
    {
        await Assert.ThrowsAsync<InvalidDataException>(
            () => fixture.CreateProvider().GetLatestReleaseAsync(
                PackageId.Parse(ProviderTestData.PackageId),
                ReleaseChannel.Stable,
                CancellationToken.None));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root;
        private readonly X509Certificate2 _certificate;

        private Fixture(string root, X509Certificate2 certificate)
        {
            _root = root;
            _certificate = certificate;
        }

        public string ManifestPath =>
            Path.Combine(_root, StableManifestName);

        public string SignaturePath =>
            ManifestSignatureConventions.GetDetachedSignaturePath(ManifestPath);

        public string TrustPath =>
            Path.Combine(_root, TrustFileName);

        public static async Task<Fixture> CreateAsync()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "turkuaz-signed-manifest-test",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(root);
            var certificate = CreateCertificate();
            var fixture = new Fixture(root, certificate);

            try
            {
                var manifest = Encoding.UTF8.GetBytes(
                    ProviderTestData.Manifest(ReleaseChannel.Stable, FixtureVersion));

                await File.WriteAllBytesAsync(fixture.ManifestPath, manifest);
                await File.WriteAllBytesAsync(
                    fixture.SignaturePath,
                    Sign(manifest, certificate));

                var pinnedSha256 = certificate.GetCertHashString(
                    HashAlgorithmName.SHA256).ToLowerInvariant();

                await fixture.WriteTrustStoreAsync(pinnedSha256);
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        public async Task WriteTrustStoreAsync(string certificateSha256)
        {
            var templatePath = Path.Combine(
                AppContext.BaseDirectory,
                TrustTemplateDirectory,
                TrustTemplateName);

            var template = await File.ReadAllTextAsync(templatePath);
            if (!template.Contains(CertificatePinToken, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Signed manifest test trust template is missing its pin token.");
            }

            var policy = template.Replace(
                CertificatePinToken,
                certificateSha256,
                StringComparison.Ordinal);

            await File.WriteAllTextAsync(TrustPath, policy);
        }

        public FileReleaseProvider CreateProvider()
        {
            var trustRepository = new YamlManifestTrustPolicyRepository(
                new YamlManifestTrustPolicyRepositoryOptions(TrustPath));

            var verifier = new CmsManifestSignatureVerifier(trustRepository);

            return new FileReleaseProvider(
                new InstallerManifestReader(),
                verifier,
                new FileReleaseProviderOptions(
                    ManifestPath,
                    Path.Combine(_root, BetaManifestName)));
        }

        public static X509Certificate2 CreateCertificate()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                FixtureSubject,
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

            return request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddDays(1));
        }

        public static byte[] Sign(
            byte[] manifest,
            X509Certificate2 certificate)
        {
            var cms = new SignedCms(
                new ContentInfo(manifest),
                detached: true);

            var signer = new CmsSigner(
                SubjectIdentifierType.IssuerAndSerialNumber,
                certificate)
            {
                IncludeOption = X509IncludeOption.EndCertOnly
            };

            cms.ComputeSignature(signer, silent: true);
            return cms.Encode();
        }

        public void Dispose()
        {
            _certificate.Dispose();
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Cleanup of temporary CI fixture is best effort.
            }
            catch (UnauthorizedAccessException)
            {
                // Do not hide the original assertion result.
            }
        }
    }
}
