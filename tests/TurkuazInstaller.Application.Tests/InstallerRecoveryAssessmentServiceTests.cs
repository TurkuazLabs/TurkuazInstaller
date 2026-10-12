// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/InstallerRecoveryAssessmentServiceTests.cs
// 📌 Amac: Salt-okunur recovery raporu tum tehlikeli checkpoint ve metadata uyusmazliklarini ayirt eder
// 📌 Modul - Test CSharp
// Version: 1.0.2
// Aciklama: Eski journal, reboot, gecersiz scope, pre-apply ve repository read failure guvenlik testi
// Bagimli Oldugu Katman: Service | Repo | Model

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using Xunit;

namespace TurkuazInstaller.Application.Tests;

public sealed class InstallerRecoveryAssessmentServiceTests
{
    private static readonly PackageId Package = PackageId.Parse("recovery-fixture");

    [Theory]
    [InlineData(InstallerOperationPhase.Applying)]
    [InlineData(InstallerOperationPhase.ApplyingIntegrations)]
    [InlineData(InstallerOperationPhase.SavingState)]
    [InlineData(InstallerOperationPhase.RemovingState)]
    [InlineData(InstallerOperationPhase.Failed)]
    [InlineData(InstallerOperationPhase.Cancelled)]
    public async Task AssessAsync_UnsafeOrAmbiguousJournal_RequiresRecovery(
        InstallerOperationPhase phase)
    {
        var entry = Journal(phase);
        var service = Create(entry, State());
        var report = await service.AssessAsync(Package, CancellationToken.None);

        Assert.Equal(InstallerRecoveryAssessmentStatus.RecoveryRequired, report.Status);
        Assert.True(report.RequiresHumanReview);
        Assert.Equal(entry.OperationId, report.OperationId);
        Assert.Equal("1.0.0", report.CommittedVersion);
        Assert.Equal("2.0.0", report.RequestedVersion);
        Assert.Equal(phase, report.JournalPhase);
    }

    [Theory]
    [InlineData(InstallerOperationPhase.AwaitingReboot)]
    [InlineData(InstallerOperationPhase.RebootResumeArmed)]
    public async Task AssessAsync_RebootCheckpoint_NeverAuthorizesManualMutation(
        InstallerOperationPhase phase)
    {
        var report = await Create(Journal(phase), State()).AssessAsync(
            Package, CancellationToken.None);
        Assert.Equal(InstallerRecoveryAssessmentStatus.PendingRebootResume, report.Status);
        Assert.True(report.RequiresHumanReview);
    }

    [Theory]
    [InlineData(InstallerOperationPhase.Verifying)]
    [InlineData(InstallerOperationPhase.Staging)]
    public async Task AssessAsync_PreApplyFailedCheckpoint_ReportsNoMutationHold(
        InstallerOperationPhase interruptedPhase)
    {
        var entry = Journal(
            InstallerOperationPhase.Failed,
            interruptedPhase: interruptedPhase);
        var report = await Create(entry, State()).AssessAsync(
            Package, CancellationToken.None);

        Assert.Equal(InstallerRecoveryAssessmentStatus.NoMutationHold, report.Status);
        Assert.False(report.RequiresHumanReview);
        Assert.Equal(interruptedPhase, report.InterruptedPhase);
    }

    [Fact]
    public async Task AssessAsync_NoJournal_ReportsCommittedStateWithoutClaimingFileIntegrity()
    {
        var report = await Create(null, State()).AssessAsync(
            Package, CancellationToken.None);
        Assert.Equal(InstallerRecoveryAssessmentStatus.NoJournal, report.Status);
        Assert.Null(report.OperationId);
        Assert.Null(report.JournalPhase);
        Assert.Equal("1.0.0", report.CommittedVersion);
    }

    [Fact]
    public async Task AssessAsync_DifferentTarget_RequiresMetadataReview()
    {
        var entry = Journal(
            InstallerOperationPhase.Failed,
            target: "C:/Apps/NotTheCommittedInstall",
            interruptedPhase: InstallerOperationPhase.Verifying);
        var report = await Create(entry, State()).AssessAsync(
            Package, CancellationToken.None);
        Assert.Equal(InstallerRecoveryAssessmentStatus.MetadataConflict, report.Status);
        Assert.True(report.RequiresHumanReview);
    }

    [Fact]
    public async Task AssessAsync_UpdateJournalWithoutCommittedState_IsConflict()
    {
        var report = await Create(Journal(InstallerOperationPhase.Applying), null)
            .AssessAsync(Package, CancellationToken.None);
        Assert.Equal(InstallerRecoveryAssessmentStatus.MetadataConflict, report.Status);
        Assert.True(report.RequiresHumanReview);
    }

    [Fact]
    public async Task AssessAsync_MissingStateForFreshInstall_IsNotFalseConflict()
    {
        var entry = Journal(InstallerOperationPhase.Applying) with
        {
            Operation = InstallerOperationType.Install
        };
        var report = await Create(entry, null).AssessAsync(
            Package, CancellationToken.None);
        Assert.Equal(InstallerRecoveryAssessmentStatus.RecoveryRequired, report.Status);
    }

    [Fact]
    public async Task AssessAsync_RepositoryReadError_MustNotBecomeNoJournal()
    {
        var service = new InstallerRecoveryAssessmentService(
            new ReadOnlyStateRepository(State()),
            new ReadOnlyJournalRepository(null, failRead: true));
        await Assert.ThrowsAsync<IOException>(
            () => service.AssessAsync(Package, CancellationToken.None));
    }


    [Theory]
    [InlineData(InstallerOperationPhase.Failed, InstallerOperationPhase.Applying)]
    [InlineData(InstallerOperationPhase.Failed, InstallerOperationPhase.ApplyingIntegrations)]
    [InlineData(InstallerOperationPhase.Failed, InstallerOperationPhase.SavingState)]
    [InlineData(InstallerOperationPhase.Failed, InstallerOperationPhase.RemovingState)]
    [InlineData(InstallerOperationPhase.Cancelled, InstallerOperationPhase.Applying)]
    [InlineData(InstallerOperationPhase.Cancelled, InstallerOperationPhase.ApplyingIntegrations)]
    [InlineData(InstallerOperationPhase.Cancelled, InstallerOperationPhase.SavingState)]
    [InlineData(InstallerOperationPhase.Cancelled, InstallerOperationPhase.RemovingState)]
    public async Task AssessAsync_FailedOrCancelledAfterMutation_RequiresRecovery(
        InstallerOperationPhase finalPhase,
        InstallerOperationPhase interruptedPhase)
    {
        var journal = Journal(finalPhase, interruptedPhase: interruptedPhase);
        var report = await Create(journal, State()).AssessAsync(
            Package, CancellationToken.None);

        Assert.Equal(InstallerRecoveryAssessmentStatus.RecoveryRequired, report.Status);
        Assert.True(report.RequiresHumanReview);
        Assert.Equal(journal.OperationId, report.OperationId);
        Assert.Equal(interruptedPhase, report.InterruptedPhase);
    }

    [Theory]
    [InlineData(InstallerOperationPhase.Failed)]
    [InlineData(InstallerOperationPhase.Cancelled)]
    public async Task AssessAsync_InterruptedBeforeMutation_DoesNotInventRecoveryHold(
        InstallerOperationPhase finalPhase)
    {
        var journal = Journal(
            finalPhase, interruptedPhase: InstallerOperationPhase.Verifying);
        var report = await Create(journal, State()).AssessAsync(
            Package, CancellationToken.None);

        Assert.Equal(InstallerRecoveryAssessmentStatus.NoMutationHold, report.Status);
        Assert.False(report.RequiresHumanReview);
    }

    [Fact]
    public async Task AssessAsync_WrongJournalPackage_ReportsMetadataConflict()
    {
        var foreign = PackageId.Parse("other-recovery-package");
        var journal = Journal(
            InstallerOperationPhase.Verifying) with { PackageId = foreign };
        var report = await Create(journal, State()).AssessAsync(
            Package, CancellationToken.None);

        Assert.Equal(InstallerRecoveryAssessmentStatus.MetadataConflict, report.Status);
        Assert.True(report.RequiresHumanReview);
        Assert.Equal(journal.OperationId, report.OperationId);
    }

    [Fact]
    public async Task AssessAsync_WrongCommittedPackage_ReportsMetadataConflict()
    {
        var foreign = PackageId.Parse("other-recovery-package");
        var state = State() with { PackageId = foreign };
        var report = await Create(Journal(InstallerOperationPhase.Verifying), state)
            .AssessAsync(Package, CancellationToken.None);

        Assert.Equal(InstallerRecoveryAssessmentStatus.MetadataConflict, report.Status);
        Assert.True(report.RequiresHumanReview);
    }

    [Fact]
    public async Task AssessAsync_NoJournalWithForeignCommittedState_IsMetadataConflict()
    {
        var foreign = PackageId.Parse("other-recovery-package");
        var state = State() with { PackageId = foreign };
        var report = await Create(null, state).AssessAsync(
            Package, CancellationToken.None);

        Assert.Equal(InstallerRecoveryAssessmentStatus.MetadataConflict, report.Status);
        Assert.True(report.RequiresHumanReview);
        Assert.Null(report.OperationId);
    }

    [Fact]
    public async Task AssessAsync_StateReadError_DoesNotMasqueradeAsNoJournal()
    {
        var service = new InstallerRecoveryAssessmentService(
            new ReadOnlyStateRepository(null, failRead: true),
            new ReadOnlyJournalRepository(null));

        await Assert.ThrowsAsync<IOException>(
            () => service.AssessAsync(Package, CancellationToken.None));
    }

    private static InstallerRecoveryAssessmentService Create(
        InstallerOperationJournalEntry? journal,
        InstalledPackageState? state) => new(
            new ReadOnlyStateRepository(state),
            new ReadOnlyJournalRepository(journal));

    private static InstalledPackageState State() => new(
        Package, SemanticVersion.Parse("1.0.0"),
        ReleaseChannel.Stable, "C:/Apps/Fixture");

    private static InstallerOperationJournalEntry Journal(
        InstallerOperationPhase phase,
        string target = "C:/Apps/Fixture",
        InstallerOperationPhase? interruptedPhase = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new InstallerOperationJournalEntry(
            Guid.NewGuid(), Package, InstallerOperationType.Update,
            "2.0.0", target, phase, now, now, null,
            InterruptedPhase: interruptedPhase);
    }

    private sealed class ReadOnlyJournalRepository : IInstallerOperationJournalRepository
    {
        private readonly InstallerOperationJournalEntry? _entry;
        private readonly bool _failRead;

        public ReadOnlyJournalRepository(
            InstallerOperationJournalEntry? entry, bool failRead = false)
        {
            _entry = entry;
            _failRead = failRead;
        }

        public Task<InstallerOperationJournalEntry?> GetAsync(
            PackageId packageId, CancellationToken cancellationToken)
        {
            if (_failRead)
            {
                throw new IOException("Simulated missing recovery evidence.");
            }
            return Task.FromResult(_entry);
        }

        public Task SaveAsync(
            InstallerOperationJournalEntry entry, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Assessment must not write a journal.");

        public Task DeleteAsync(
            PackageId packageId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Assessment must not delete a journal.");
    }

    private sealed class ReadOnlyStateRepository : IInstallStateRepository
    {
        private readonly InstalledPackageState? _state;
        private readonly bool _failRead;

        public ReadOnlyStateRepository(
            InstalledPackageState? state, bool failRead = false)
        {
            _state = state;
            _failRead = failRead;
        }

        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId, CancellationToken cancellationToken)
            => _failRead
                ? Task.FromException<InstalledPackageState?>(
                    new IOException("Simulated committed-state read failure."))
                : Task.FromResult(_state);

        public Task<IReadOnlyList<InstalledPackageState>> ListAsync(
            CancellationToken cancellationToken)
            => throw new InvalidOperationException("Assessment must not scan packages.");

        public Task SaveAsync(
            InstalledPackageState state, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Assessment must not save state.");

        public Task DeleteAsync(
            PackageId packageId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Assessment must not delete state.");
    }
}
