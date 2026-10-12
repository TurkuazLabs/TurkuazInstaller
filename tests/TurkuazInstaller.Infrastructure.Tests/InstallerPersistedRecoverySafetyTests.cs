// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/InstallerPersistedRecoverySafetyTests.cs
// 📌 Amac: Farkli service instance'larinda gercek JSON journal ve dosya durumu uzerinden crash safety hold test eder
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Apply sonrasi fake crash, committed state/disk uyusmazligi ve ikinci islemde fail-closed
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Domain.Verification;
using TurkuazInstaller.Infrastructure.Operations;
using TurkuazInstaller.Infrastructure.Repositories;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class InstallerPersistedRecoverySafetyTests
{
    private const string LabDigest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string InstalledVersion = "1.0.0";
    private const string PendingVersion = "2.0.0";

    [Fact]
    public async Task UpdateAsync_AfterDiskMutationFails_NextServiceRefusesRetryAndUninstall()
    {
        var root = CreateRoot();
        try
        {
            var packageId = PackageId.Parse("crash-recovery-fixture");
            var appRoot = Path.Combine(root, "app");
            var marker = Path.Combine(appRoot, "version.txt");
            var userData = Path.Combine(root, "user-data", "preferences.txt");
            Directory.CreateDirectory(appRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(userData)!);
            await File.WriteAllTextAsync(marker, InstalledVersion);
            await File.WriteAllTextAsync(userData, "USER_PREFERENCES_MUST_SURVIVE");

            var stateDirectory = Path.Combine(root, "committed-state");
            var journalDirectory = Path.Combine(root, "operation-journal");
            var state = new JsonInstallStateRepository(
                new JsonInstallStateRepositoryOptions(stateDirectory));
            var journal = new JsonOperationJournalRepository(
                new JsonOperationJournalRepositoryOptions(journalDirectory));
            var original = new InstalledPackageState(
                packageId, SemanticVersion.Parse(InstalledVersion),
                ReleaseChannel.Stable, appRoot);
            await state.SaveAsync(original, CancellationToken.None);
            var attemptedRelease = CreateRelease(packageId, PendingVersion);

            // The adapter intentionally writes part of the new version
            // before throwing. The real workflow may then persist Failed,
            // while the committed JSON state must stay on V1.
            var interruptedEngine = new FileMarkerPackageEngine(
                marker, throwAfterMutation: true);
            var firstService = CreateService(interruptedEngine, state, journal);
            await Assert.ThrowsAsync<IOException>(() =>
                firstService.UpdateAsync(
                    attemptedRelease, original, Path.Combine(root, "staging"),
                    null, CancellationToken.None));

            Assert.Equal(PendingVersion, await File.ReadAllTextAsync(marker));
            Assert.Equal(InstalledVersion,
                (await state.GetAsync(packageId, CancellationToken.None))!
                    .Version.ToString());

            var failed = await journal.GetAsync(packageId, CancellationToken.None);
            Assert.NotNull(failed);
            Assert.Equal(InstallerOperationPhase.Failed, failed.Phase);
            Assert.Equal(InstallerOperationPhase.Applying, failed.InterruptedPhase);
            var operationId = failed.OperationId;

            // Simulates a new process opening the previously persisted data.
            var restartedState = new JsonInstallStateRepository(
                new JsonInstallStateRepositoryOptions(stateDirectory));
            var restartedJournal = new JsonOperationJournalRepository(
                new JsonOperationJournalRepositoryOptions(journalDirectory));
            // Assessment is purely observational. Inspect the actual JSON
            // bytes before and after it reads the persisted crash evidence.
            var stateDocument = Directory.GetFiles(stateDirectory, "*.json").Single();
            var journalDocument = Directory.GetFiles(journalDirectory, "*.json").Single();
            var stateBytesBefore = await File.ReadAllBytesAsync(stateDocument);
            var journalBytesBefore = await File.ReadAllBytesAsync(journalDocument);
            var assessment = await new InstallerRecoveryAssessmentService(
                restartedState, restartedJournal).AssessAsync(
                    packageId, CancellationToken.None);

            Assert.Equal(
                InstallerRecoveryAssessmentStatus.RecoveryRequired, assessment.Status);
            Assert.True(assessment.RequiresHumanReview);
            Assert.Equal(operationId, assessment.OperationId);
            Assert.Equal(InstallerOperationPhase.Failed, assessment.JournalPhase);
            Assert.Equal(InstallerOperationPhase.Applying, assessment.InterruptedPhase);
            Assert.Equal(InstalledVersion, assessment.CommittedVersion);
            Assert.Equal(PendingVersion, assessment.RequestedVersion);
            Assert.Equal(stateBytesBefore, await File.ReadAllBytesAsync(stateDocument));
            Assert.Equal(journalBytesBefore, await File.ReadAllBytesAsync(journalDocument));
            Assert.Equal(PendingVersion, await File.ReadAllTextAsync(marker));
            Assert.Equal("USER_PREFERENCES_MUST_SURVIVE",
                await File.ReadAllTextAsync(userData));

            var retryEngine = new FileMarkerPackageEngine(marker);
            var secondService = CreateService(
                retryEngine, restartedState, restartedJournal);

            var recovery = await Assert.ThrowsAsync<InstallerRecoveryRequiredException>(
                () => secondService.UpdateAsync(
                    attemptedRelease, original, Path.Combine(root, "staging"),
                    null, CancellationToken.None));
            Assert.Equal(packageId, recovery.PackageId);
            Assert.Equal(InstallerOperationPhase.Applying, recovery.InterruptedPhase);

            // Blind uninstall would be equally dangerous while disk and
            // the committed version differ.
            await Assert.ThrowsAsync<InstallerRecoveryRequiredException>(
                () => secondService.UninstallAsync(
                    original, null, CancellationToken.None));

            Assert.Equal(0, retryEngine.StageCalls);
            Assert.Equal(0, retryEngine.ApplyCalls);
            Assert.Equal(0, retryEngine.UninstallCalls);
            Assert.Equal(PendingVersion, await File.ReadAllTextAsync(marker));
            Assert.Equal("USER_PREFERENCES_MUST_SURVIVE",
                await File.ReadAllTextAsync(userData));
            Assert.Equal(InstalledVersion,
                (await restartedState.GetAsync(packageId, CancellationToken.None))!
                    .Version.ToString());
            Assert.Equal(operationId,
                (await restartedJournal.GetAsync(packageId, CancellationToken.None))!
                    .OperationId);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task UpdateAsync_OrphanedApplyCheckpointOnDisk_FailsClosedWithoutMutation()
    {
        var root = CreateRoot();
        try
        {
            var packageId = PackageId.Parse("crash-recovery-fixture");
            var appRoot = Path.Combine(root, "app");
            Directory.CreateDirectory(appRoot);
            var marker = Path.Combine(appRoot, "version.txt");
            await File.WriteAllTextAsync(marker, PendingVersion);
            var state = new JsonInstallStateRepository(
                new JsonInstallStateRepositoryOptions(Path.Combine(root, "state")));
            var journal = new JsonOperationJournalRepository(
                new JsonOperationJournalRepositoryOptions(Path.Combine(root, "journal")));
            var committed = new InstalledPackageState(
                packageId, SemanticVersion.Parse(InstalledVersion),
                ReleaseChannel.Stable, appRoot);
            await state.SaveAsync(committed, CancellationToken.None);
            var now = DateTimeOffset.UtcNow;
            var interrupted = new InstallerOperationJournalEntry(
                Guid.NewGuid(), packageId, InstallerOperationType.Update,
                PendingVersion, appRoot, InstallerOperationPhase.Applying,
                now, now, null);
            await journal.SaveAsync(interrupted, CancellationToken.None);

            var engine = new FileMarkerPackageEngine(marker);
            var service = CreateService(engine, state, journal);
            await Assert.ThrowsAsync<InstallerRecoveryRequiredException>(
                () => service.UpdateAsync(
                    CreateRelease(packageId, PendingVersion),
                    committed, Path.Combine(root, "staging"), null,
                    CancellationToken.None));

            Assert.Equal(0, engine.StageCalls);
            Assert.Equal(0, engine.ApplyCalls);
            Assert.Equal(PendingVersion, await File.ReadAllTextAsync(marker));
            Assert.Equal(InstalledVersion,
                (await state.GetAsync(packageId, CancellationToken.None))!
                    .Version.ToString());
            Assert.Equal(interrupted,
                await journal.GetAsync(packageId, CancellationToken.None));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static InstallerWorkflowService CreateService(
        IPackageEngine engine,
        JsonInstallStateRepository state,
        JsonOperationJournalRepository journal)
        => new(
            new LabDownloader(),
            new LabVerifier(),
            engine,
            state,
            operationJournal: journal);

    private static PackageRelease CreateRelease(
        PackageId packageId, string version)
        => new(
            packageId, SemanticVersion.Parse(version),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri("https://example.invalid/crash-fixture-full.nupkg"),
                ArtifactDigest.ParseSha256(LabDigest), 1024));

    private sealed class LabDownloader : IArtifactDownloader
    {
        public Task<string> DownloadAsync(
            ArtifactDescriptor artifact,
            string stagingDirectory,
            CancellationToken cancellationToken)
            => Task.FromResult(Path.Combine(stagingDirectory, "verified-lab.nupkg"));
    }

    private sealed class LabVerifier : IArtifactVerifier
    {
        public Task<VerificationResult> VerifyAsync(
            string artifactPath,
            ArtifactDescriptor artifact,
            CancellationToken cancellationToken)
            => Task.FromResult(VerificationResult.Passed());
    }

    private sealed class FileMarkerPackageEngine : IPackageEngine
    {
        private readonly string _markerPath;
        private readonly bool _throwAfterMutation;

        public FileMarkerPackageEngine(
            string markerPath, bool throwAfterMutation = false)
        {
            _markerPath = markerPath;
            _throwAfterMutation = throwAfterMutation;
        }

        public int StageCalls { get; private set; }
        public int ApplyCalls { get; private set; }
        public int UninstallCalls { get; private set; }

        public Task<PackageStage> StageAsync(
            PackageStageRequest request,
            string verifiedArtifactPath,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            StageCalls++;
            return Task.FromResult(new PackageStage(
                request.Release.PackageId, request.Release.Version,
                PackageArtifactKind.VelopackFullPackage, verifiedArtifactPath));
        }

        public async Task ApplyAsync(
            InstallPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
        {
            ApplyCalls++;
            await File.WriteAllTextAsync(
                _markerPath, stage.Version.ToString(), cancellationToken);
            if (_throwAfterMutation)
            {
                throw new IOException("Simulated interruption after disk mutation.");
            }
        }

        public Task RepairAsync(
            RepairPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task RollbackAsync(
            RollbackPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task UninstallAsync(
            UninstallPlan plan,
            CancellationToken cancellationToken)
        {
            UninstallCalls++;
            return Task.CompletedTask;
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "TurkuazInstallerRecoveryDiskLab",
            Guid.NewGuid().ToString("N"));
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
}
