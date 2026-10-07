// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/InstallerRecoveryTests.cs
// 📌 Amac: Installer workflow hata durumlarinda state commit edilmemesi ve rollback state guvenligini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.1
// Aciklama: Verification, package apply ve Windows integration failure senaryolarinda state commit sinirini ve retry-guvenli recovery davranisini test eder
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Integrations;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Integrations;
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
    public async Task InstallAsync_IntegrationFailure_DoesNotCommitState()
    {
        var engine =
            new TrackingPackageEngine();

        var stateRepository =
            new TrackingStateRepository();

        var integrationManager =
            new TrackingWindowsIntegrationManager
            {
                FailuresRemaining = 1
            };

        var service =
            CreateService(
                engine,
                stateRepository,
                VerificationResult.Passed(),
                integrationManager);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.InstallAsync(
                CreateRelease(
                    "1.0.0",
                    "Example-Setup.exe"),
                "C:/Apps/Example",
                "C:/Temp/TurkuazInstaller",
                null,
                CancellationToken.None));

        Assert.Equal(
            1,
            engine.ApplyCalls);
        Assert.Equal(
            1,
            integrationManager.ApplyCalls);
        Assert.Equal(
            0,
            stateRepository.SaveCalls);
        Assert.Null(
            stateRepository.CurrentState);
    }

    [Fact]
    public async Task UpdateAsync_IntegrationFailure_PreservesStateAndRetryCanCommit()
    {
        var packageId =
            PackageId.Parse(
                "example-app");

        var currentRelease =
            CreateRelease(
                "1.0.0",
                "Example-1.0.0-full.nupkg");

        var updateRelease =
            CreateRelease(
                "2.0.0",
                "Example-2.0.0-full.nupkg");

        var currentState =
            new InstalledPackageState(
                packageId,
                currentRelease.Version,
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var stateRepository =
            new TrackingStateRepository(
                currentState);

        var integrationManager =
            new TrackingWindowsIntegrationManager
            {
                FailuresRemaining = 1
            };

        var service =
            CreateService(
                new TrackingPackageEngine(),
                stateRepository,
                VerificationResult.Passed(),
                integrationManager);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(
                updateRelease,
                currentState,
                "C:/Temp/TurkuazInstaller",
                null,
                CancellationToken.None));

        Assert.Equal(
            0,
            stateRepository.SaveCalls);
        Assert.Equal(
            currentRelease.Version,
            stateRepository.CurrentState?.Version);

        await service.UpdateAsync(
            updateRelease,
            currentState,
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.Equal(
            2,
            integrationManager.ApplyCalls);
        Assert.Equal(
            1,
            stateRepository.SaveCalls);
        Assert.Equal(
            updateRelease.Version,
            stateRepository.CurrentState?.Version);
    }

    [Fact]
    public async Task RollbackAsync_IntegrationFailure_PreservesCurrentState()
    {
        var packageId =
            PackageId.Parse(
                "example-app");

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
            new TrackingStateRepository(
                currentState);

        var integrationManager =
            new TrackingWindowsIntegrationManager
            {
                FailuresRemaining = 1
            };

        var service =
            CreateService(
                new TrackingPackageEngine(),
                stateRepository,
                VerificationResult.Passed(),
                integrationManager);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RollbackAsync(
                currentRelease,
                previousRelease,
                currentState,
                "C:/Temp/TurkuazInstaller",
                null,
                CancellationToken.None));

        Assert.Equal(
            1,
            integrationManager.ApplyCalls);
        Assert.Equal(
            0,
            stateRepository.SaveCalls);
        Assert.Equal(
            currentRelease.Version,
            stateRepository.CurrentState?.Version);
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

    [Fact]
    public async Task UninstallAsync_Success_RemovesInstalledState()
    {
        var packageId =
            PackageId.Parse("example-app");

        var currentState =
            new InstalledPackageState(
                packageId,
                SemanticVersion.Parse("1.0.0"),
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var engine =
            new TrackingPackageEngine();

        var stateRepository =
            new TrackingStateRepository(
                currentState);

        var service =
            CreateService(
                engine,
                stateRepository,
                VerificationResult.Passed());

        await service.UninstallAsync(
            currentState,
            null,
            CancellationToken.None);

        Assert.Equal(
            1,
            engine.UninstallCalls);

        Assert.Equal(
            1,
            stateRepository.DeleteCalls);

        Assert.Null(
            stateRepository.CurrentState);
    }

    private static InstallerWorkflowService CreateService(
        TrackingPackageEngine engine,
        TrackingStateRepository repository,
        VerificationResult verificationResult,
        IWindowsIntegrationManager? integrationManager = null)
    {
        return new InstallerWorkflowService(
            new StubDownloader(),
            new StubVerifier(
                verificationResult),
            engine,
            repository,
            windowsIntegrationManager:
                integrationManager);
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
                1024),
            PackageInstallPolicy.LegacyDefault,
            new PackageRollbackPolicy(
                Supported: true,
                PreviousVersionRequired: true));
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

        public int UninstallCalls { get; private set; }

        public Exception? ApplyException { get; init; }

        public Exception? RollbackException { get; init; }

        public Task<PackageStage> StageAsync(
            PackageStageRequest request,
            string verifiedArtifactPath,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            StageCalls++;

            var release =
                request.Release;

            var artifactKind =
                request.Artifact.Uri.AbsolutePath
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
            UninstallCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingWindowsIntegrationManager
        : IWindowsIntegrationManager
    {
        public int ApplyCalls
        {
            get;
            private set;
        }

        public int FailuresRemaining
        {
            get;
            set;
        }

        public Task ApplyAsync(
            PackageId packageId,
            string targetPath,
            WindowsIntegrationPolicy policy,
            CancellationToken cancellationToken)
        {
            ApplyCalls++;

            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;

                throw new InvalidOperationException(
                    "Simulated Windows integration failure.");
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            PackageId packageId,
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

        public int DeleteCalls { get; private set; }

        public InstalledPackageState? CurrentState { get; private set; }

        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CurrentState);
        }

        public Task<IReadOnlyList<InstalledPackageState>> ListAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<InstalledPackageState> states =
                CurrentState is null
                    ? Array.Empty<InstalledPackageState>()
                    : new[]
                    {
                        CurrentState
                    };

            return Task.FromResult(
                states);
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
            DeleteCalls++;
            CurrentState = null;
            return Task.CompletedTask;
        }
    }
}
