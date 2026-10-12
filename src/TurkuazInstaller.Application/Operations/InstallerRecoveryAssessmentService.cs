// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerRecoveryAssessmentService.cs
// 📌 Amac: Kurulum journal ve committed state kayitlarini salt-okunur inceleyerek kurtarma ihtiyacini bildirir
// 📌 Modul - Service CSharp
// Version: 1.1.0
// Aciklama: Kayit uyumsuzlugunu ve mutation checkpointlerini fail-closed siniflandirir, hicbir kaydi degistirmez
// Bagimli Oldugu Katman: Service | Repo | Domain

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Application.Operations;

public sealed class InstallerRecoveryAssessmentService
{
    private readonly IInstallStateRepository _installedState;
    private readonly IInstallerOperationJournalRepository _journal;

    public InstallerRecoveryAssessmentService(
        IInstallStateRepository installedState,
        IInstallerOperationJournalRepository journal)
    {
        _installedState = installedState ??
            throw new ArgumentNullException(nameof(installedState));
        _journal = journal ??
            throw new ArgumentNullException(nameof(journal));
    }

    public async Task<InstallerRecoveryAssessment> AssessAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        cancellationToken.ThrowIfCancellationRequested();

        // Both reads are observations, not a transaction or a disk integrity
        // check. Never unlock a mutation based on this diagnostic snapshot.
        var entry = await _journal.GetAsync(packageId, cancellationToken)
            .ConfigureAwait(false);
        var state = await _installedState.GetAsync(packageId, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return new InstallerRecoveryAssessment(
                packageId,
                state is not null && state.PackageId != packageId
                    ? InstallerRecoveryAssessmentStatus.MetadataConflict
                    : InstallerRecoveryAssessmentStatus.NoJournal,
                null, null, null, null, state?.Version.ToString(),
                null, state?.TargetPath);
        }

        var mismatch = InstallerRecoveryPolicy.HasMetadataConflict(
            packageId, entry, state);

        var status = mismatch
            ? InstallerRecoveryAssessmentStatus.MetadataConflict
            : InstallerRecoveryPolicy.IsRebootPhase(entry.Phase)
                ? InstallerRecoveryAssessmentStatus.PendingRebootResume
                : InstallerRecoveryPolicy.RequiresExplicitRecovery(entry)
                    ? InstallerRecoveryAssessmentStatus.RecoveryRequired
                    : InstallerRecoveryAssessmentStatus.NoMutationHold;

        return new InstallerRecoveryAssessment(
            packageId, status,
            entry.OperationId, entry.Phase, entry.InterruptedPhase,
            entry.Version, state?.Version.ToString(),
            entry.TargetPath, state?.TargetPath);
    }
}
