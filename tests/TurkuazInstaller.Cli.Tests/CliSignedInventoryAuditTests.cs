// 📄 Dosya Yolu: /tests/TurkuazInstaller.Cli.Tests/CliSignedInventoryAuditTests.cs
// 📌 Amac: Read-only CLI audit pinned CMS ile dogrulanmis baseline, state ve disk kontrolunu sinar
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Gercek RSA CMS signature, wrong pin, wrong release, unknown current ve UserData korunmasi
// Bagimli Oldugu Katman: Service | Repo | Tool

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Cli.Services;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Tools;
using Xunit;

namespace TurkuazInstaller.Cli.Tests;

public sealed class CliSignedInventoryAuditTests
{
    private static readonly PackageId Package = PackageId.Parse("signed-cli-audit-fixture");
    private static readonly byte[] VersionBytes = Encoding.UTF8.GetBytes("2.0.0");
    private static readonly byte[] ArtifactBytes = Encoding.UTF8.GetBytes("signed-release-artifact");

    [Fact]
    public async Task AuditAsync_RealPinnedCmsAndIndependentStateProduceReadOnlyDiagnostics()
    {
        var root = CreateLabRoot();
        using var certificate = CreateCertificate();
        try
        {
            var current = Path.Combine(root, "current");
            Directory.CreateDirectory(current);
            Directory.CreateDirectory(Path.Combine(root, "UserData"));
            await File.WriteAllTextAsync(Path.Combine(current, "version.txt"), "2.0.0");
            await File.WriteAllTextAsync(Path.Combine(current, "unexpected.txt"), "extra");
            await File.WriteAllTextAsync(Path.Combine(root, "UserData", "prefs.json"), "private");
            var userData = await File.ReadAllBytesAsync(
                Path.Combine(root, "UserData", "prefs.json"));
            var unknown = await File.ReadAllBytesAsync(
                Path.Combine(current, "unexpected.txt"));
            var release = Release();
            var bytes = Inventory(release);
            var audit = CreateAudit(release, certificate,
                new InstalledPackageState(
                    Package, SemanticVersion.Parse("2.0.0"),
                    ReleaseChannel.Stable, root));

            var result = await audit.AssessAsync(
                Package, ReleaseChannel.Stable,
                bytes, Sign(bytes, certificate), CancellationToken.None);

            Assert.Equal(new[] { "current/unexpected.txt" },
                result.UnattributedCurrentFiles);
            Assert.Equal(InstallerPayloadFileStatus.MatchesSuppliedDigest,
                Assert.Single(result.HashEvidence.Files).Status);
            Assert.Equal(userData, await File.ReadAllBytesAsync(
                Path.Combine(root, "UserData", "prefs.json")));
            Assert.Equal(unknown, await File.ReadAllBytesAsync(
                Path.Combine(current, "unexpected.txt")));
        }
        finally
        {
            DeleteLabRoot(root);
        }
    }

    [Fact]
    public async Task AuditAsync_WrongPinnedCertificateNeverReadsInstalledPayload()
    {
        using var certificate = CreateCertificate();
        var release = Release();
        var bytes = Inventory(release);
        var scope = new RejectAnyScopeRead();
        var audit = CreateAudit(
            release, certificate,
            new InstalledPackageState(
                Package, SemanticVersion.Parse("2.0.0"),
                ReleaseChannel.Stable, Path.GetTempPath()),
            wrongPin: true, scopeTool: scope);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => audit.AssessAsync(
                Package, ReleaseChannel.Stable,
                bytes, Sign(bytes, certificate), CancellationToken.None));
        Assert.Equal(0, scope.ReadCalls);
    }

    [Fact]
    public async Task AuditAsync_AlteredInventoryCannotReadInstalledPayload()
    {
        using var certificate = CreateCertificate();
        var release = Release();
        var original = Inventory(release);
        var altered = original.ToArray();
        altered[^2] ^= 0x01;
        var scope = new RejectAnyScopeRead();
        var audit = CreateAudit(
            release, certificate,
            new InstalledPackageState(
                Package, SemanticVersion.Parse("2.0.0"),
                ReleaseChannel.Stable, Path.GetTempPath()),
            scopeTool: scope);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => audit.AssessAsync(
                Package, ReleaseChannel.Stable,
                altered, Sign(original, certificate), CancellationToken.None));
        Assert.Equal(0, scope.ReadCalls);
    }

    [Fact]
    public async Task AuditAsync_ReleaseVersionConflictFailsBeforeInventoryDiskScan()
    {
        using var certificate = CreateCertificate();
        var release = Release();
        var bytes = Inventory(release);
        var scope = new RejectAnyScopeRead();
        var audit = CreateAudit(
            release, certificate,
            new InstalledPackageState(
                Package, SemanticVersion.Parse("1.0.0"),
                ReleaseChannel.Stable, Path.GetTempPath()),
            scopeTool: scope);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => audit.AssessAsync(
                Package, ReleaseChannel.Stable,
                bytes, Sign(bytes, certificate), CancellationToken.None));
        Assert.Equal(0, scope.ReadCalls);
    }

    [Fact]
    public async Task AuditAsync_InterruptedApplyJournalBlocksBeforeInstalledDiskRead()
    {
        using var certificate = CreateCertificate();
        var release = Release();
        var bytes = Inventory(release);
        var scope = new RejectAnyScopeRead();
        var journal = new TestJournalRepository(
            Entry(Path.GetTempPath(), InstallerOperationPhase.Applying));
        var audit = CreateAudit(
            release, certificate,
            new InstalledPackageState(
                Package, SemanticVersion.Parse("2.0.0"),
                ReleaseChannel.Stable, Path.GetTempPath()),
            scopeTool: scope, journalRepository: journal);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => audit.AssessAsync(
                Package, ReleaseChannel.Stable,
                bytes, Sign(bytes, certificate), CancellationToken.None));
        Assert.Contains("journal", exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, scope.ReadCalls);
        Assert.Equal(0, journal.WriteCalls);
    }

    [Fact]
    public async Task AuditAsync_JournalAppearingDuringScanRefusesCleanResult()
    {
        var root = CreateLabRoot();
        using var certificate = CreateCertificate();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "current"));
            await File.WriteAllTextAsync(
                Path.Combine(root, "current", "version.txt"), "2.0.0");
            var journal = new TestJournalRepository(
                Entry(root, InstallerOperationPhase.Applying),
                appearOnRead: 2);
            var release = Release();
            var bytes = Inventory(release);
            var audit = CreateAudit(
                release, certificate,
                new InstalledPackageState(
                    Package, SemanticVersion.Parse("2.0.0"),
                    ReleaseChannel.Stable, root),
                journalRepository: journal);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => audit.AssessAsync(
                    Package, ReleaseChannel.Stable,
                    bytes, Sign(bytes, certificate), CancellationToken.None));
            Assert.True(journal.ReadCalls >= 2);
            Assert.Equal(0, journal.WriteCalls);
            Assert.Equal("2.0.0", await File.ReadAllTextAsync(
                Path.Combine(root, "current", "version.txt")));
        }
        finally
        {
            DeleteLabRoot(root);
        }
    }

    [Theory]
    [InlineData("audit")]
    [InlineData("audit --package test --target C:/Apps")]
    [InlineData("audit --package test --manifest foo --manifest bar --inventory baz")]
    [InlineData("audit --package test --manifest foo --inventory bar --silent")]
    public async Task AuditCommand_InvalidOrMutationLikeArgsFailClosed(string args)
    {
        var tokens = args.Split(' ');
        var result = await CliSignedInventoryAuditCommand.RunAsync(
            tokens, CancellationToken.None);
        Assert.Equal(CliExitCode.InvalidInvocation, result);
    }

    private static CliSignedInventoryAuditService CreateAudit(
        PackageRelease release, X509Certificate2 certificate,
        InstalledPackageState state, bool wrongPin = false,
        IInstalledPayloadScopeTool? scopeTool = null,
        IInstallerOperationJournalRepository? journalRepository = null)
    {
        var expectedPin = certificate.GetCertHashString(
            HashAlgorithmName.SHA256).ToLowerInvariant();
        if (wrongPin)
        {
            expectedPin = new string('0', 64);
        }
        var signer = new CmsManifestSignatureVerifier(
            new TestPolicyRepository(
                new ManifestTrustPolicy(certificate.Subject, expectedPin)));
        return new CliSignedInventoryAuditService(
            new TestReleaseProvider(release),
            signer,
            new TestStateRepository(state),
            journalRepository ?? new TestJournalRepository(),
            new InstallerPayloadScopeAssessmentService(
                scopeTool ?? new InstalledPayloadScopeTool(),
                new InstallerPayloadEvidenceService(
                    new InstalledPayloadHashTool())));
    }

    private static PackageRelease Release()
        => new(
            Package, SemanticVersion.Parse("2.0.0"), ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri("https://example.invalid/signed-2.0.0-full.nupkg"),
                Sha(ArtifactBytes), ArtifactBytes.Length));

    private static byte[] Inventory(PackageRelease release)
        => JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = 1,
            package_id = release.PackageId.Value,
            version = release.Version.ToString(),
            channel = "stable",
            artifact_sha256 = release.Artifact.Digest.Sha256,
            files = new[]
            {
                new { path = "current/version.txt", sha256 = Sha(VersionBytes).Sha256 }
            }
        });

    private static ArtifactDigest Sha(byte[] bytes)
        => ArtifactDigest.ParseSha256(
            Convert.ToHexString(SHA256.HashData(bytes)));

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=TurkuazInstaller CLI Audit LAB", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
    }

    private static byte[] Sign(byte[] bytes, X509Certificate2 cert)
    {
        var cms = new SignedCms(new ContentInfo(bytes), detached: true);
        cms.ComputeSignature(new CmsSigner(
            SubjectIdentifierType.IssuerAndSerialNumber, cert)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        }, silent: true);
        return cms.Encode();
    }

    private static string CreateLabRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "TurkuazInstaller-CLI-Audit-LAB",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteLabRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestReleaseProvider : IReleaseProvider
    {
        private readonly PackageRelease _release;
        public TestReleaseProvider(PackageRelease release) => _release = release;
        public Task<PackageRelease?> GetLatestReleaseAsync(
            PackageId packageId, ReleaseChannel channel,
            CancellationToken cancellationToken)
            => Task.FromResult<PackageRelease?>(_release);
    }

    private sealed class TestPolicyRepository : IManifestTrustPolicyRepository
    {
        private readonly ManifestTrustPolicy _policy;
        public TestPolicyRepository(ManifestTrustPolicy policy) => _policy = policy;
        public Task<ManifestTrustPolicy?> GetAsync(
            PackageId packageId, CancellationToken cancellationToken)
            => Task.FromResult<ManifestTrustPolicy?>(_policy);
    }

    private sealed class TestStateRepository : IInstallStateRepository
    {
        private readonly InstalledPackageState _state;
        public TestStateRepository(InstalledPackageState state) => _state = state;
        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId, CancellationToken cancellationToken)
            => Task.FromResult<InstalledPackageState?>(_state);
        public Task<IReadOnlyList<InstalledPackageState>> ListAsync(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<InstalledPackageState>>(
                new[] { _state });
        public Task SaveAsync(
            InstalledPackageState state, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Audit must never commit state.");
        public Task DeleteAsync(
            PackageId packageId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Audit must never delete state.");
    }

    private static InstallerOperationJournalEntry Entry(
        string path, InstallerOperationPhase phase)
        => new(
            Guid.NewGuid(), Package, InstallerOperationType.Update,
            "2.0.0", path, phase, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow, null);

    private sealed class TestJournalRepository :
        IInstallerOperationJournalRepository
    {
        private readonly InstallerOperationJournalEntry? _entry;
        private readonly int _appearOnRead;

        public TestJournalRepository(
            InstallerOperationJournalEntry? entry = null,
            int appearOnRead = 1)
        {
            _entry = entry;
            _appearOnRead = appearOnRead;
        }

        public int ReadCalls { get; private set; }
        public int WriteCalls { get; private set; }

        public Task<InstallerOperationJournalEntry?> GetAsync(
            PackageId packageId, CancellationToken cancellationToken)
        {
            ReadCalls++;
            return Task.FromResult(
                ReadCalls >= _appearOnRead ? _entry : null);
        }

        public Task SaveAsync(
            InstallerOperationJournalEntry entry,
            CancellationToken cancellationToken)
        {
            WriteCalls++;
            throw new InvalidOperationException("Audit must never save journal.");
        }

        public Task DeleteAsync(
            PackageId packageId, CancellationToken cancellationToken)
        {
            WriteCalls++;
            throw new InvalidOperationException("Audit must never delete journal.");
        }
    }

    private sealed class RejectAnyScopeRead : IInstalledPayloadScopeTool
    {
        public int ReadCalls { get; private set; }
        public Task<IReadOnlyList<string>> ListCurrentFilesAsync(
            string installRoot, CancellationToken cancellationToken)
        {
            ReadCalls++;
            throw new InvalidOperationException(
                "An untrusted audit must not read install directories.");
        }
    }
}
