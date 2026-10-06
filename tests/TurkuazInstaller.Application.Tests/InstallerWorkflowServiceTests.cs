// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/InstallerWorkflowServiceTests.cs
// 📌 Amac: InstallerWorkflowService install pipeline sirasi ve state kaydini fake portlarla unit test eder
// 📌 Modul - Test CSharp
// Version: 1.4.0
// Aciklama: Download/verify/apply zincirine ek olarak version policy, Windows integration reconcile ve cleanup failure isolation davranislarini dogrular
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

        public Task<string> DownloadAsync(
            ArtifactDescriptor artifact,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            DownloadCalls++;

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
