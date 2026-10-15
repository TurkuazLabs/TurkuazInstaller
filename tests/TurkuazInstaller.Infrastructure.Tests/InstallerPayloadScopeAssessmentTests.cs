// Dosya: /tests/TurkuazInstaller.Infrastructure.Tests/InstallerPayloadScopeAssessmentTests.cs
// Amac: Imzali baseline'den current kapsam fazlaligi ve state identity uyusmazligini salt-okunur sinar
// Modul - Test CSharp
// Version: 1.0.1
// Aciklama: Extra current files, UserData ayrimi, root payload hash ve symlink detection
// Bagimli Oldugu Katman: Service | Tool | Domain

using System.Security.Cryptography;
using System.Text;
using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Infrastructure.Tools;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class InstallerPayloadScopeAssessmentTests
{
    private static readonly PackageId Package = PackageId.Parse("scope-lab-package");
    private static readonly SemanticVersion Version = SemanticVersion.Parse("2.0.0");

    [Fact]
    public async Task AssessAsync_ReportsUnattributedCurrentFilesWithoutMutatingAnyFile()
    {
        var root = CreateRoot();
        try
        {
            var current = Path.Combine(root, "current");
            Directory.CreateDirectory(Path.Combine(current, "plugins"));
            Directory.CreateDirectory(Path.Combine(root, "UserData"));
            await File.WriteAllTextAsync(Path.Combine(current, "version.txt"), "2.0.0");
            await File.WriteAllTextAsync(Path.Combine(current, "plugins", "unknown.dll"), "foreign");
            await File.WriteAllTextAsync(Path.Combine(root, "UserData", "prefs.json"), "private");
            await File.WriteAllTextAsync(Path.Combine(root, "other.log"), "unclaimed");
            await File.WriteAllTextAsync(Path.Combine(root, "Update.exe"), "updater");
            var userDataBefore = await File.ReadAllBytesAsync(
                Path.Combine(root, "UserData", "prefs.json"));
            var currentBefore = await File.ReadAllBytesAsync(
                Path.Combine(current, "plugins", "unknown.dll"));

            var assessment = await CreateService().AssessAsync(
                Package, Version, ReleaseChannel.Stable,
                new[]
                {
                    Expected("current/version.txt", "2.0.0"),
                    Expected("Update.exe", "updater")
                }, State(root), root, CancellationToken.None);

            Assert.Equal(new[] { "current/plugins/unknown.dll" },
                assessment.UnattributedCurrentFiles);
            Assert.All(assessment.HashEvidence.Files, file =>
                Assert.Equal(
                    InstallerPayloadFileStatus.MatchesSuppliedDigest,
                    file.Status));
            Assert.Equal(userDataBefore, await File.ReadAllBytesAsync(
                Path.Combine(root, "UserData", "prefs.json")));
            Assert.Equal(currentBefore, await File.ReadAllBytesAsync(
                Path.Combine(current, "plugins", "unknown.dll")));
            Assert.True(File.Exists(Path.Combine(root, "other.log")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task AssessAsync_EqualParsedSemanticVersionsMustNotConflictAfterStateReload()
    {
        var root = CreateRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "current"));
            await File.WriteAllTextAsync(
                Path.Combine(root, "current", "version.txt"), "2.0.0");
            // Verified signed inventory and persisted state independently
            // deserialize versions; they must be compared by value.
            var independentVersion = SemanticVersion.Parse("2.0.0");
            Assert.False(ReferenceEquals(independentVersion, Version));

            var result = await CreateService().AssessAsync(
                Package, independentVersion, ReleaseChannel.Stable,
                new[] { Expected("current/version.txt", "2.0.0") },
                State(root), root, CancellationToken.None);

            Assert.Empty(result.UnattributedCurrentFiles);
            Assert.Equal(InstallerPayloadFileStatus.MatchesSuppliedDigest,
                Assert.Single(result.HashEvidence.Files).Status);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task AssessAsync_MissingExpectedFileAndNoExtrasDoNotGrantPositiveEvidence()
    {
        var root = CreateRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "current"));
            var result = await CreateService().AssessAsync(
                Package, Version, ReleaseChannel.Stable,
                new[] { Expected("current/missing.dll", "expected") },
                State(root), root, CancellationToken.None);

            Assert.Empty(result.UnattributedCurrentFiles);
            Assert.Equal(InstallerPayloadFileStatus.Missing,
                Assert.Single(result.HashEvidence.Files).Status);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task AssessAsync_RejectsDifferentVersionBeforeReadingDisk()
    {
        var scope = new SpyScopeTool();
        var service = new InstallerPayloadScopeAssessmentService(
            scope, new InstallerPayloadEvidenceService(new NeverHashTool()));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.AssessAsync(
                Package, SemanticVersion.Parse("3.0.0"),
                ReleaseChannel.Stable,
                new[] { Expected("current/a", "a") },
                State(Path.GetTempPath()), Path.GetTempPath(),
                CancellationToken.None));
        Assert.Equal(0, scope.ReadCalls);
    }

    [Fact]
    public async Task AssessAsync_RejectsDifferentInstallTargetBeforeReadingDisk()
    {
        var root = CreateRoot();
        try
        {
            var scope = new SpyScopeTool();
            var service = new InstallerPayloadScopeAssessmentService(
                scope, new InstallerPayloadEvidenceService(new NeverHashTool()));
            await Assert.ThrowsAsync<InvalidDataException>(
                () => service.AssessAsync(
                    Package, Version, ReleaseChannel.Stable,
                    new[] { Expected("current/a", "a") },
                    State(Path.Combine(root, "different")), root,
                    CancellationToken.None));
            Assert.Equal(0, scope.ReadCalls);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task AssessAsync_RejectsSymlinkInsideCurrentInsteadOfClaimingOwnership()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        var root = CreateRoot();
        var outside = CreateRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "current"));
            var secret = Path.Combine(outside, "secret.txt");
            await File.WriteAllTextAsync(secret, "no scan outside");
            File.CreateSymbolicLink(
                Path.Combine(root, "current", "linked.txt"), secret);
            await Assert.ThrowsAsync<IOException>(
                () => CreateService().AssessAsync(
                    Package, Version, ReleaseChannel.Stable,
                    new[] { Expected("current/linked.txt", "ignore") },
                    State(root), root, CancellationToken.None));
        }
        finally
        {
            DeleteRoot(root);
            DeleteRoot(outside);
        }
    }

    [Fact]
    public async Task ListCurrentFilesAsync_RejectsSymlinkCurrentDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        var root = CreateRoot();
        var outside = CreateRoot();
        try
        {
            Directory.CreateSymbolicLink(
                Path.Combine(root, "current"), outside);
            await Assert.ThrowsAsync<IOException>(
                () => new InstalledPayloadScopeTool().ListCurrentFilesAsync(
                    root, CancellationToken.None));
        }
        finally
        {
            DeleteRoot(root);
            DeleteRoot(outside);
        }
    }

    private static InstallerPayloadScopeAssessmentService CreateService()
        => new(new InstalledPayloadScopeTool(),
            new InstallerPayloadEvidenceService(new InstalledPayloadHashTool()));

    private static InstalledPackageState State(string target)
        => new(Package, Version, ReleaseChannel.Stable, target);

    private static InstallerExpectedPayloadFile Expected(
        string path, string bytes)
        => new(path, ArtifactDigest.ParseSha256(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bytes)))));

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "TurkuazInstaller-Scope-LAB", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class SpyScopeTool : IInstalledPayloadScopeTool
    {
        public int ReadCalls { get; private set; }
        public Task<IReadOnlyList<string>> ListCurrentFilesAsync(
            string root, CancellationToken cancellationToken)
        {
            ReadCalls++;
            throw new IOException("Scope was unexpectedly read.");
        }
    }

    private sealed class NeverHashTool : IInstalledPayloadHashTool
    {
        public Task<string?> ComputeSha256Async(
            string root, string relativePath, CancellationToken cancellationToken)
            => throw new IOException("Hash was unexpectedly read.");
    }
}
