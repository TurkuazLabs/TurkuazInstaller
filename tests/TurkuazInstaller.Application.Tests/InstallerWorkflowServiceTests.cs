// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/InstallerWorkflowServiceTests.cs
// 📌 Amac: InstallerWorkflowService install pipeline sirasi ve state kaydini fake portlarla unit test eder
// 📌 Modul - Test CSharp
// Version: 1.5.0
// Aciklama: Download/verify/apply zincirine exact-base delta secimi, reconstructed full verify ve pre-apply full fallback testlerini ekler
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Integrations;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Operations;
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
    public async Task InstallAsync_AcquiresPackageOperationLock()
    {
        var operationLock =
            new StubOperationLock();

        var release =
            CreateRelease();

        var service =
            new InstallerWorkflowService(
                new StubDownloader(),
                new StubVerifier(),
                new StubPackageEngine(),
                new StubStateRepository(),
                operationLock: operationLock);

        await service.InstallAsync(
            release,
            "C:/Apps/Example",
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.Equal(
            release.PackageId,
            operationLock.AcquiredPackageId);

        Assert.True(
            operationLock.LeaseDisposed);
    }

    [Fact]
    public async Task InstallAsync_WithDiagnostics_ClearsJournalAndLogsCompletion()
    {
        var journal =
            new StubOperationJournal();

        var logger =
            new StubEventLogger();

        var release =
            CreateRelease();

        var service =
            new InstallerWorkflowService(
                new StubDownloader(),
                new StubVerifier(),
                new StubPackageEngine(),
                new StubStateRepository(),
                operationJournal: journal,
                eventLogger: logger);

        await service.InstallAsync(
            release,
            "C:/Apps/Example",
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.Null(
            journal.CurrentEntry);

        Assert.True(
            journal.SaveCalls >= 5);

        Assert.Equal(
            1,
            journal.DeleteCalls);

        Assert.Contains(
            logger.Events,
            entry =>
                entry.EventName ==
                "operation.completed");
    }

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



    [Fact]
    public async Task InstallAsync_WithWindowsIntegration_ReconcilesTypedPolicy()
    {
        var stateRepository =
            new StubStateRepository();

        var integrationManager =
            new StubWindowsIntegrationManager();

        var release =
            CreateReleaseWithIntegration();

        var service =
            new InstallerWorkflowService(
                new StubDownloader(),
                new StubVerifier(),
                new StubPackageEngine(),
                stateRepository,
                windowsIntegrationManager:
                    integrationManager);

        await service.InstallAsync(
            release,
            "C:/Apps/Example",
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.NotNull(
            stateRepository.SavedState);

        Assert.Equal(
            1,
            integrationManager.ApplyCalls);

        Assert.Equal(
            release.PackageId,
            integrationManager.LastPackageId);

        Assert.Equal(
            "main",
            Assert.Single(
                integrationManager.LastPolicy!.Shortcuts)
                .Id);

        Assert.Equal(
            "example-app",
            Assert.Single(
                integrationManager.LastPolicy.Protocols)
                .Scheme);
    }

    [Fact]
    public async Task UninstallAsync_IntegrationCleanupFailure_DeletesStateAndLogsWarning()
    {
        var stateRepository =
            new StubStateRepository
            {
                SavedState =
                    new InstalledPackageState(
                        PackageId.Parse(
                            "example-app"),
                        SemanticVersion.Parse(
                            "1.0.0"),
                        ReleaseChannel.Stable,
                        "C:/Apps/Example")
            };

        var logger =
            new StubEventLogger();

        var integrationManager =
            new StubWindowsIntegrationManager
            {
                RemoveException =
                    new IOException(
                        "Simulated integration cleanup failure.")
            };

        var service =
            new InstallerWorkflowService(
                new StubDownloader(),
                new StubVerifier(),
                new StubPackageEngine(),
                stateRepository,
                eventLogger:
                    logger,
                windowsIntegrationManager:
                    integrationManager);

        await service.UninstallAsync(
            stateRepository.SavedState,
            null,
            CancellationToken.None);

        Assert.Equal(
            1,
            integrationManager.RemoveCalls);

        Assert.Equal(
            1,
            stateRepository.DeleteCalls);

        Assert.Null(
            stateRepository.SavedState);

        Assert.Contains(
            logger.Events,
            entry =>
                entry.EventName ==
                    "integration.cleanup_failed" &&
                entry.Level ==
                    InstallerEventLevel.Warning);
    }

    [Fact]
    public async Task UpdateAsync_SkippedVersion_BlocksBeforeJournalAndDownload()
    {
        var downloader =
            new StubDownloader();

        var journal =
            new StubOperationJournal();

        var packageId =
            PackageId.Parse(
                "example-app");

        var currentState =
            new InstalledPackageState(
                packageId,
                SemanticVersion.Parse(
                    "1.0.0"),
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var release =
            new PackageRelease(
                packageId,
                SemanticVersion.Parse(
                    "1.1.0"),
                ReleaseChannel.Stable,
                new ArtifactDescriptor(
                    new Uri(
                        "https://example.invalid/Example-Setup.exe"),
                    ArtifactDigest.ParseSha256(
                        Digest),
                    1024));

        var policy =
            new VersionUpdatePolicy(
                packageId,
                ReleaseChannel.Stable,
                null,
                new[]
                {
                    SemanticVersion.Parse(
                        "1.1.0")
                });

        var service =
            new InstallerWorkflowService(
                downloader,
                new StubVerifier(),
                new StubPackageEngine(),
                new StubStateRepository(),
                operationJournal: journal,
                versionPolicyRepository:
                    new StubVersionPolicyRepository(
                        policy));

        var exception =
            await Assert.ThrowsAsync<InstallerVersionPolicyException>(
                () =>
                    service.UpdateAsync(
                        release,
                        currentState,
                        "C:/Temp/TurkuazInstaller",
                        null,
                        CancellationToken.None));

        Assert.Equal(
            VersionUpdatePolicyDecision.Skipped,
            exception.Decision);

        Assert.Equal(
            0,
            downloader.DownloadCalls);

        Assert.Equal(
            0,
            journal.SaveCalls);
    }


    [Fact]
    public async Task UpdateAsync_ExactSmallerDelta_UsesDeltaAndVerifiesReconstructedFull()
    {
        var downloader =
            new StubDownloader();

        var verifier =
            new StubVerifier();

        var packageEngine =
            new StubPackageEngine();

        var release =
            CreateDeltaRelease();

        var currentState =
            new InstalledPackageState(
                release.PackageId,
                SemanticVersion.Parse(
                    "1.0.0"),
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var service =
            new InstallerWorkflowService(
                downloader,
                verifier,
                packageEngine,
                new StubStateRepository());

        await service.UpdateAsync(
            release,
            currentState,
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.Equal(
            release.DeltaArtifact!.Artifact,
            Assert.Single(
                downloader.DownloadedArtifacts));

        Assert.Equal(
            2,
            verifier.ExpectedArtifacts.Count);

        Assert.Equal(
            release.DeltaArtifact.Artifact,
            verifier.ExpectedArtifacts[0]);

        Assert.Equal(
            release.Artifact,
            verifier.ExpectedArtifacts[1]);

        var stageRequest =
            Assert.Single(
                packageEngine.StageRequests);

        Assert.Equal(
            release.DeltaArtifact.Artifact,
            stageRequest.Artifact);

        Assert.Equal(
            currentState.TargetPath,
            stageRequest.InstalledTargetPath);
    }

    [Fact]
    public async Task UpdateAsync_DeltaStageFailure_FallsBackToFullBeforeApply()
    {
        var downloader =
            new StubDownloader();

        var packageEngine =
            new StubPackageEngine
            {
                FailDeltaStage = true
            };

        var logger =
            new StubEventLogger();

        var release =
            CreateDeltaRelease();

        var currentState =
            new InstalledPackageState(
                release.PackageId,
                SemanticVersion.Parse(
                    "1.0.0"),
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var stateRepository =
            new StubStateRepository();

        var service =
            new InstallerWorkflowService(
                downloader,
                new StubVerifier(),
                packageEngine,
                stateRepository,
                eventLogger:
                    logger);

        await service.UpdateAsync(
            release,
            currentState,
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.Equal(
            2,
            downloader.DownloadedArtifacts.Count);

        Assert.Equal(
            release.DeltaArtifact!.Artifact,
            downloader.DownloadedArtifacts[0]);

        Assert.Equal(
            release.Artifact,
            downloader.DownloadedArtifacts[1]);

        Assert.Equal(
            2,
            packageEngine.StageRequests.Count);

        Assert.Equal(
            release.Artifact,
            packageEngine.StageRequests[1].Artifact);

        Assert.NotNull(
            stateRepository.SavedState);

        Assert.Contains(
            logger.Events,
            entry =>
                entry.EventName ==
                "artifact.delta_fallback");
    }

    private static PackageRelease CreateDeltaRelease()
    {
        var fullArtifact =
            new ArtifactDescriptor(
                new Uri(
                    "https://example.invalid/Example-1.1.0-full.nupkg"),
                ArtifactDigest.ParseSha256(
                    Digest),
                2048);

        var deltaArtifact =
            new PackageDeltaArtifact(
                SemanticVersion.Parse(
                    "1.0.0"),
                new ArtifactDescriptor(
                    new Uri(
                        "https://example.invalid/Example-1.1.0-delta.nupkg"),
                    ArtifactDigest.ParseSha256(
                        Digest),
                    512));

        return new PackageRelease(
            PackageId.Parse(
                "example-app"),
            SemanticVersion.Parse(
                "1.1.0"),
            ReleaseChannel.Stable,
            fullArtifact,
            PackageInstallPolicy.LegacyDefault,
            PackageRollbackPolicy.Disabled,
            deltaArtifact);
    }


    private static PackageRelease CreateReleaseWithIntegration()
    {
        return new PackageRelease(
            PackageId.Parse(
                "example-app"),
            SemanticVersion.Parse(
                "1.0.0"),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri(
                    "https://example.invalid/Example-Setup.exe"),
                ArtifactDigest.ParseSha256(
                    Digest),
                1024),
            new PackageInstallPolicy(
                PackageInstallMode.Full,
                "C:/Apps/Example",
                Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
                Array.Empty<string>(),
                new WindowsIntegrationPolicy(
                    new[]
                    {
                        new WindowsShortcutIntegration(
                            "main",
                            "Example App",
                            WindowsShortcutLocation.StartMenu,
                            "ExampleApp.exe")
                    },
                    new[]
                    {
                        new WindowsProtocolIntegration(
                            "example-app",
                            "ExampleApp.exe")
                    })),
            PackageRollbackPolicy.Disabled);
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


    private sealed class StubWindowsIntegrationManager
        : IWindowsIntegrationManager
    {
        public int ApplyCalls
        {
            get;
            private set;
        }

        public PackageId? LastPackageId
        {
            get;
            private set;
        }

        public WindowsIntegrationPolicy? LastPolicy
        {
            get;
            private set;
        }

        public int RemoveCalls
        {
            get;
            private set;
        }

        public Exception? RemoveException
        {
            get;
            init;
        }

        public Task ApplyAsync(
            PackageId packageId,
            string targetPath,
            WindowsIntegrationPolicy policy,
            CancellationToken cancellationToken)
        {
            ApplyCalls++;
            LastPackageId = packageId;
            LastPolicy = policy;

            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            RemoveCalls++;

            if (RemoveException is not null)
            {
                throw RemoveException;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubDownloader
        : IArtifactDownloader
    {
        public int DownloadCalls
        {
            get;
            private set;
        }

        public List<ArtifactDescriptor> DownloadedArtifacts
        {
            get;
        } = new();

        public Task<string> DownloadAsync(
            ArtifactDescriptor artifact,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            DownloadCalls++;
            DownloadedArtifacts.Add(
                artifact);

            return Task.FromResult(
                "C:/Temp/Example-Setup.exe");
        }
    }

    private sealed class StubVerifier
        : IArtifactVerifier
    {
        public List<ArtifactDescriptor> ExpectedArtifacts
        {
            get;
        } = new();

        public Task<VerificationResult> VerifyAsync(
            string artifactPath,
            ArtifactDescriptor expectedArtifact,
            CancellationToken cancellationToken)
        {
            ExpectedArtifacts.Add(
                expectedArtifact);

            return Task.FromResult(
                VerificationResult.Passed());
        }
    }

    private sealed class StubPackageEngine
        : IPackageEngine
    {
        private const string FullPackageExtension =
            ".nupkg";

        private const string ReconstructedArtifactPath =
            "C:/Temp/Example-1.1.0-full.nupkg";

        public bool FailDeltaStage
        {
            get;
            init;
        }

        public List<PackageStageRequest> StageRequests
        {
            get;
        } = new();

        public Task<PackageStage> StageAsync(
            PackageStageRequest request,
            string verifiedArtifactPath,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            StageRequests.Add(
                request);

            var deltaArtifact =
                request.Release.DeltaArtifact;

            var isDelta =
                deltaArtifact is not null &&
                Equals(
                    deltaArtifact.Artifact,
                    request.Artifact);

            if (
                FailDeltaStage &&
                isDelta)
            {
                throw new PackageEngineException(
                    PackageEngineOperation.Stage,
                    "Simulated delta stage failure.");
            }

            var artifactKind =
                request.Artifact.Uri.AbsolutePath.EndsWith(
                    FullPackageExtension,
                    StringComparison.OrdinalIgnoreCase)
                    ? PackageArtifactKind.VelopackFullPackage
                    : PackageArtifactKind.VelopackSetup;

            return Task.FromResult(
                new PackageStage(
                    request.Release.PackageId,
                    request.Release.Version,
                    isDelta
                        ? PackageArtifactKind.VelopackFullPackage
                        : artifactKind,
                    isDelta
                        ? ReconstructedArtifactPath
                        : verifiedArtifactPath));
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

        public Task UninstallAsync(
            UninstallPlan plan,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubOperationJournal
        : IInstallerOperationJournalRepository
    {
        public int SaveCalls { get; private set; }

        public int DeleteCalls { get; private set; }

        public InstallerOperationJournalEntry? CurrentEntry
        {
            get;
            private set;
        }

        public Task<InstallerOperationJournalEntry?> GetAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CurrentEntry);
        }

        public Task SaveAsync(
            InstallerOperationJournalEntry entry,
            CancellationToken cancellationToken)
        {
            SaveCalls++;
            CurrentEntry = entry;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            DeleteCalls++;
            CurrentEntry = null;
            return Task.CompletedTask;
        }
    }

    private sealed class StubEventLogger
        : IInstallerEventLogger
    {
        public List<InstallerEventEntry> Events { get; } =
            new();

        public Task WriteAsync(
            InstallerEventEntry entry,
            CancellationToken cancellationToken)
        {
            Events.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubOperationLock
        : IInstallerOperationLock
    {
        public PackageId? AcquiredPackageId
        {
            get;
            private set;
        }

        public bool LeaseDisposed
        {
            get;
            private set;
        }

        public Task<IAsyncDisposable> AcquireAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            AcquiredPackageId =
                packageId;

            return Task.FromResult<IAsyncDisposable>(
                new StubLease(
                    this));
        }

        private sealed class StubLease
            : IAsyncDisposable
        {
            private readonly StubOperationLock _owner;

            public StubLease(
                StubOperationLock owner)
            {
                _owner = owner;
            }

            public ValueTask DisposeAsync()
            {
                _owner.LeaseDisposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class StubVersionPolicyRepository
        : IVersionUpdatePolicyRepository
    {
        private readonly VersionUpdatePolicy? _policy;

        public StubVersionPolicyRepository(
            VersionUpdatePolicy? policy)
        {
            _policy = policy;
        }

        public Task<VersionUpdatePolicy?> GetAsync(
            PackageId packageId,
            ReleaseChannel channel,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _policy);
        }
    }

    private sealed class StubStateRepository
        : IInstallStateRepository
    {
        public InstalledPackageState? SavedState { get; set; }

        public int DeleteCalls
        {
            get;
            private set;
        }

        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<InstalledPackageState?>(
                null);
        }

        public Task<IReadOnlyList<InstalledPackageState>> ListAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<InstalledPackageState> states =
                SavedState is null
                    ? Array.Empty<InstalledPackageState>()
                    : new[]
                    {
                        SavedState
                    };

            return Task.FromResult(
                states);
        }

        public Task SaveAsync(
            InstalledPackageState state,
            CancellationToken cancellationToken)
        {
            SavedState = state;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            DeleteCalls++;
            SavedState = null;
            return Task.CompletedTask;
        }
    }
}
