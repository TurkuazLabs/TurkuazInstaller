// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/SignedFileManifestIntegrationTests.cs
// 📌 Amac: Yerel manifest providerindan gercek detached CMS ve harici YAML trust pini ile signed manifest kabul/reddini dogrular
// 📌 Modul - Test CSharp
// Version: 0.2.0
// Aciklama: Test sirasinda ephemeral RSA sertifikasi ile valid, tampered, yanlis pin, farkli signer, eksik signature ve eksik policy kontrolleri
// Bagimli Oldugu Katman: Service | Repo | Tool | Config

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Infrastructure.Artifacts;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstallAsync_SignedManifestPayloadHashGateControlsMutation(
        bool wrongPayloadDigest)
    {
        using var fixture = await Fixture.CreateAsync();
        await fixture.WriteSignedWorkflowManifestAsync(wrongPayloadDigest);
        var release = await fixture.CreateProvider().GetLatestReleaseAsync(
            PackageId.Parse(ProviderTestData.PackageId),
            ReleaseChannel.Stable,
            CancellationToken.None);
        Assert.NotNull(release);
        var engine = new TrackingPackageEngine();
        var states = new TrackingStateRepository();
        using var http = new HttpClient();
        var workflow = new InstallerWorkflowService(
            new DefaultArtifactDownloader(http),
            new Sha256ArtifactVerifier(),
            engine,
            states);

        if (wrongPayloadDigest)
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => workflow.InstallAsync(
                    release, fixture.InstallPath, fixture.StagingPath,
                    null, CancellationToken.None));
            Assert.Equal(0, engine.StageCalls);
            Assert.Equal(0, engine.ApplyCalls);
            Assert.Null(states.Current);
            Assert.Equal(0, states.SaveCalls);
        }
        else
        {
            await workflow.InstallAsync(
                release, fixture.InstallPath, fixture.StagingPath,
                null, CancellationToken.None);
            Assert.Equal(1, engine.StageCalls);
            Assert.Equal(1, engine.ApplyCalls);
            Assert.Equal(1, states.SaveCalls);
            Assert.Equal(release.Version, states.Current?.Version);
        }
    }

    private sealed class TrackingPackageEngine : IPackageEngine
    {
        public int StageCalls { get; private set; }
        public int ApplyCalls { get; private set; }

        public Task<PackageStage> StageAsync(
            PackageStageRequest request, string verifiedArtifactPath,
            string stagingDirectory, CancellationToken cancellationToken)
        {
            StageCalls++;
            return Task.FromResult(new PackageStage(
                request.Release.PackageId, request.Release.Version,
                PackageArtifactKind.VelopackSetup, verifiedArtifactPath));
        }

        public Task ApplyAsync(
            InstallPlan plan, PackageStage stage, CancellationToken cancellationToken)
        {
            ApplyCalls++;
            return Task.CompletedTask;
        }

        public Task RepairAsync(
            RepairPlan plan, PackageStage stage, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task RollbackAsync(
            RollbackPlan plan, PackageStage stage, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UninstallAsync(
            UninstallPlan plan, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class TrackingStateRepository : IInstallStateRepository
    {
        public int SaveCalls { get; private set; }
        public InstalledPackageState? Current { get; private set; }

        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId, CancellationToken cancellationToken)
            => Task.FromResult(Current);

        public Task<IReadOnlyList<InstalledPackageState>> ListAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<InstalledPackageState>>(
                Current is null ? Array.Empty<InstalledPackageState>() : new[] { Current });

        public Task SaveAsync(
            InstalledPackageState state, CancellationToken cancellationToken)
        {
            SaveCalls++;
            Current = state;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            PackageId packageId, CancellationToken cancellationToken)
        {
            Current = null;
            return Task.CompletedTask;
        }
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

        public string InstallPath => Path.Combine(_root, "installed-app");
        public string StagingPath => Path.Combine(_root, "staging");

        public async Task WriteSignedWorkflowManifestAsync(bool wrongPayloadDigest)
        {
            var artifactPath = Path.Combine(_root, "Fixture-Setup.exe");
            var payload = Encoding.UTF8.GetBytes("isolated-ci-fixture-not-executable");
            await File.WriteAllBytesAsync(artifactPath, payload);

            var realDigest = Convert.ToHexString(
                SHA256.HashData(payload)).ToLowerInvariant();
            var declaredDigest = wrongPayloadDigest
                ? new string('0', 64)
                : realDigest;
            var artifactUri = new UriBuilder(Uri.UriSchemeFile, string.Empty)
            {
                Path = artifactPath
            }.Uri.AbsoluteUri;

            // Payload Authenticode intentionally omitted in this fixture.
            // This test only spans CMS manifest trust and SHA-256 gating.
            var manifestYaml = $"""
schema_version: 1
package:
  id: {{ProviderTestData.PackageId}}
  version: {{FixtureVersion}}
  channel: stable
artifact:
  uri: {{artifactUri}}
  sha256: {{declaredDigest}}
  size_bytes: {{payload.Length}}
install:
  mode: full
  target: C:/Apps/Example
  preserve_paths:
    - UserData
rollback:
  supported: true
  previous_version_required: true
""";

            var manifest = Encoding.UTF8.GetBytes(manifestYaml);
            await File.WriteAllBytesAsync(ManifestPath, manifest);
            await File.WriteAllBytesAsync(
                SignaturePath,
                Sign(manifest, _certificate));
        }

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
