// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerRecoveryAssessment.cs
// 📌 Amac: Kurtarma gereksinimi icin veri degistirmeyen typed durum raporunu tasir
// 📌 Modul - Model CSharp
// Version: 1.0.0
// Aciklama: Journal ve committed state bilgisini kanit sanmadan raporlar; dosya dogrulamaz
// Bagimli Oldugu Katman: Service | Domain

using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Application.Operations;

public enum InstallerRecoveryAssessmentStatus
{
    NoJournal,
    NoMutationHold,
    PendingRebootResume,
    RecoveryRequired,
    MetadataConflict
}

public sealed record InstallerRecoveryAssessment(
    PackageId PackageId,
    InstallerRecoveryAssessmentStatus Status,
    Guid? OperationId,
    InstallerOperationPhase? JournalPhase,
    InstallerOperationPhase? InterruptedPhase,
    string? RequestedVersion,
    string? CommittedVersion,
    string? JournalTarget,
    string? CommittedTarget)
{
    // Deliberately NOT an authorization to retry, repair, clear a journal,
    // or assert that installed files match either recorded version.
    public bool RequiresHumanReview =>
        Status is InstallerRecoveryAssessmentStatus.RecoveryRequired or
            InstallerRecoveryAssessmentStatus.PendingRebootResume or
            InstallerRecoveryAssessmentStatus.MetadataConflict;
}
