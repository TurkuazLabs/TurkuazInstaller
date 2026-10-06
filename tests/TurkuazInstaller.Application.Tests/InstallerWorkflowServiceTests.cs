// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/InstallerWorkflowServiceTests.cs
// 📌 Amac: InstallerWorkflowService install pipeline sirasi ve state kaydini fake portlarla unit test eder
// 📌 Modul - Test CSharp
// Version: 1.2.0
// Aciklama: Download/verify/apply zincirine ek olarak version-policy-blocked update'in journal/download baslatmadigini dogrular
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Artifacts;
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
        public InstalledPackageState? SavedState { get; private set; }

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
            SavedState = null;
            return Task.CompletedTask;
        }
    }
}
