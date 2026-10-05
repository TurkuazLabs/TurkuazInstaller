// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/InstallerWorkflowServiceTests.cs
// 📌 Amac: InstallerWorkflowService install pipeline sirasi ve state kaydini fake portlarla unit test eder
// 📌 Modul - Test CSharp
// Version: 0.7.0
// Aciklama: Download, verify, stage, apply ve state save koordinasyonunu dis sistem kullanmadan dogrular
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Domain.Verification;
using Xunit;

namespace TurkuazInstaller.Application.Tests;

public sealed class InstallerWorkflowServiceTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task InstallAsync_SavesInstalledState()
    {
        var stateRepository = new StubStateRepository();
        var release = CreateRelease();

        var service = new InstallerWorkflowService(
            new StubDownloader(),
            new StubVerifier(),
            new StubPackageEngine(),
            stateRepository);

        await service.InstallAsync(
            release,
            "C:/Apps/Example",
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.NotNull(stateRepository.SavedState);
        Assert.Equal(
            release.PackageId,
            stateRepository.SavedState.PackageId);
        Assert.Equal(
            release.Version,
            stateRepository.SavedState.Version);
    }

    private static PackageRelease CreateRelease()
    {
        return new PackageRelease(
            PackageId.Parse("example-app"),
            SemanticVersion.Parse("1.0.0"),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri(
                    "https://example.invalid/Example-Setup.exe"),
                ArtifactDigest.ParseSha256(Digest),
                1024));
    }

    private sealed class StubDownloader
        : IArtifactDownloader
    {
        public Task<string> DownloadAsync(
            ArtifactDescriptor artifact,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                "C:/Temp/Example-Setup.exe");
        }
    }

    private sealed class StubVerifier
        : IArtifactVerifier
    {
        public Task<VerificationResult> VerifyAsync(
            string artifactPath,
            ArtifactDescriptor expectedArtifact,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                VerificationResult.Passed());
        }
    }

    private sealed class StubPackageEngine
        : IPackageEngine
    {
        public Task<PackageStage> StageAsync(
            PackageRelease release,
            string verifiedArtifactPath,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new PackageStage(
                    release.PackageId,
                    release.Version,
                    PackageArtifactKind.VelopackSetup,
                    verifiedArtifactPath));
        }

        public Task ApplyAsync(
            InstallPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RepairAsync(
            RepairPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RollbackAsync(
            RollbackPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubStateRepository
        : IInstallStateRepository
    {
        public InstalledPackageState? SavedState { get; private set; }

        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<InstalledPackageState?>(
                null);
        }

        public Task SaveAsync(
            InstalledPackageState state,
            CancellationToken cancellationToken)
        {
            SavedState = state;
            return Task.CompletedTask;
        }
    }
}
