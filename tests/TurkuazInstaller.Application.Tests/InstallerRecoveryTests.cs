// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/InstallerRecoveryTests.cs
// 📌 Amac: Installer workflow hata durumlarinda state commit edilmemesi ve rollback state guvenligini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Verification, apply ve rollback failure senaryolarinda yarim islemin kurulu state olarak kaydedilmesini engelleyen davranisi test eder
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

public sealed class InstallerRecoveryTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task InstallAsync_VerificationFailure_DoesNotStageOrSaveState()
    {
        var engine = new TrackingPackageEngine();
        var stateRepository = new TrackingStateRepository();

        var service = CreateService(
            engine,
            stateRepository,
            VerificationResult.Failed(
                VerificationFailure.HashMismatch,
                "Hash mismatch"));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => service.InstallAsync(
                CreateRelease(
                    "1.0.0",
                    "Example-Setup.exe"),
                "C:/Apps/Example",
                "C:/Temp/TurkuazInstaller",
                null,
                CancellationToken.None));

        Assert.Equal(0, engine.StageCalls);
        Assert.Equal(0, engine.ApplyCalls);
        Assert.Equal(0, stateRepository.SaveCalls);
    }

    [Fact]
    public async Task InstallAsync_ApplyFailure_DoesNotSaveState()
    {
        var engine = new TrackingPackageEngine
        {
            ApplyException =
                new InvalidOperationException(
                    "Apply failure")
        };

        var stateRepository =
            new TrackingStateRepository();

        var service = CreateService(
            engine,
            stateRepository,
            VerificationResult.Passed());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InstallAsync(
                CreateRelease(
                    "1.0.0",
                    "Example-Setup.exe"),
                "C:/Apps/Example",
                "C:/Temp/TurkuazInstaller",
                null,
                CancellationToken.None));

        Assert.Equal(1, engine.ApplyCalls);
        Assert.Equal(0, stateRepository.SaveCalls);
    }

    [Fact]
    public async Task RollbackAsync_Failure_PreservesCurrentState()
    {
        var packageId =
            PackageId.Parse("example-app");

        var currentRelease =
            CreateRelease(
                "2.0.0",
                "Example-2.0.0-full.nupkg");

        var previousRelease =
            CreateRelease(
                "1.0.0",
                "Example-1.0.0-full.nupkg");

        var currentState =
            new InstalledPackageState(
                packageId,
                currentRelease.Version,
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var engine = new TrackingPackageEngine
        {
            RollbackException =
                new InvalidOperationException(
                    "Rollback failure")
        };

        var stateRepository =
            new TrackingStateRepository(currentState);

        var service = CreateService(
            engine,
            stateRepository,
            VerificationResult.Passed());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RollbackAsync(
                currentRelease,
                previousRelease,
                currentState,
                "C:/Temp/TurkuazInstaller",
                null,
                CancellationToken.None));

        Assert.Equal(0, stateRepository.SaveCalls);
        Assert.Equal(
            currentRelease.Version,
            stateRepository.CurrentState?.Version);
    }

    [Fact]
    public async Task RollbackAsync_Success_CommitsPreviousState()
    {
        var packageId =
            PackageId.Parse("example-app");

        var currentRelease =
            CreateRelease(
                "2.0.0",
                "Example-2.0.0-full.nupkg");

        var previousRelease =
            CreateRelease(
                "1.0.0",
                "Example-1.0.0-full.nupkg");

        var currentState =
            new InstalledPackageState(
                packageId,
                currentRelease.Version,
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var stateRepository =
            new TrackingStateRepository(currentState);

        var service = CreateService(
            new TrackingPackageEngine(),
            stateRepository,
            VerificationResult.Passed());

        await service.RollbackAsync(
            currentRelease,
            previousRelease,
            currentState,
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.Equal(1, stateRepository.SaveCalls);
        Assert.Equal(
            previousRelease.Version,
            stateRepository.CurrentState?.Version);
    }

    private static InstallerWorkflowService CreateService(
        TrackingPackageEngine engine,
        TrackingStateRepository repository,
        VerificationResult verificationResult)
    {
        return new InstallerWorkflowService(
            new StubDownloader(),
            new StubVerifier(
                verificationResult),
            engine,
            repository);
    }

    private static PackageRelease CreateRelease(
        string version,
        string fileName)
    {
        return new PackageRelease(
            PackageId.Parse("example-app"),
            SemanticVersion.Parse(version),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri(
                    string.Concat(
                        "https://example.invalid/",
                        fileName)),
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
                "C:/Temp/artifact.bin");
        }
    }

    private sealed class StubVerifier
        : IArtifactVerifier
    {
        private readonly VerificationResult _result;

        public StubVerifier(
            VerificationResult result)
        {
            _result = result;
        }

        public Task<VerificationResult> VerifyAsync(
            string artifactPath,
            ArtifactDescriptor expectedArtifact,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _result);
        }
    }

    private sealed class TrackingPackageEngine
        : IPackageEngine
    {
        public int StageCalls { get; private set; }

        public int ApplyCalls { get; private set; }

        public Exception? ApplyException { get; init; }

        public Exception? RollbackException { get; init; }

        public Task<PackageStage> StageAsync(
            PackageRelease release,
            string verifiedArtifactPath,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            StageCalls++;

            var artifactKind =
                release.Artifact.Uri.AbsolutePath
                    .EndsWith(
                        ".nupkg",
                        StringComparison.OrdinalIgnoreCase)
                    ? PackageArtifactKind.VelopackFullPackage
                    : PackageArtifactKind.VelopackSetup;

            return Task.FromResult(
                new PackageStage(
                    release.PackageId,
                    release.Version,
                    artifactKind,
                    verifiedArtifactPath));
        }

        public Task ApplyAsync(
            InstallPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
        {
            ApplyCalls++;

            if (ApplyException is not null)
            {
                throw ApplyException;
            }

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
            if (RollbackException is not null)
            {
                throw RollbackException;
            }

            return Task.CompletedTask;
        }

        public Task UninstallAsync(
            UninstallPlan plan,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingStateRepository
        : IInstallStateRepository
    {
        public TrackingStateRepository(
            InstalledPackageState? initialState = null)
        {
            CurrentState = initialState;
        }

        public int SaveCalls { get; private set; }

        public InstalledPackageState? CurrentState { get; private set; }

        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CurrentState);
        }

        public Task SaveAsync(
            InstalledPackageState state,
            CancellationToken cancellationToken)
        {
            SaveCalls++;
            CurrentState = state;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            CurrentState = null;
            return Task.CompletedTask;
        }
    }
}
