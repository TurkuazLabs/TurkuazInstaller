// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerRecoveryPolicy.cs
// 📌 Amac: Kesintiye ugrayan operasyonlarin recovery kilidini tek typed kuralla belirler
// 📌 Modul - Service CSharp
// Version: 1.1.1
// Aciklama: Workflow mutation guard ve salt-okunur recovery inceleme ayni policy'yi kullanir
// Bagimli Oldugu Katman: Service | Domain

using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.State;

namespace TurkuazInstaller.Application.Operations;

internal static class InstallerRecoveryPolicy
{
    public static bool HasMetadataConflict(
        PackageId packageId,
        InstallerOperationJournalEntry entry,
        InstalledPackageState? state)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.PackageId != packageId)
        {
            return true;
        }

        // A durable Completed checkpoint is not evidence of a partial
        // mutation. A completed uninstall legitimately has no state.
        if (entry.Phase == InstallerOperationPhase.Completed)
        {
            return false;
        }

        if (state is null)
        {
            // A clean first-time install may not have committed state yet.
            return entry.Operation != InstallerOperationType.Install;
        }

        return state.PackageId != packageId ||
            !string.Equals(
                entry.TargetPath,
                state.TargetPath,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
    }

    public static bool RequiresExplicitRecovery(
        InstallerOperationJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (IsMutationPhase(entry.Phase) ||
            IsRebootPhase(entry.Phase))
        {
            return true;
        }

        if (entry.Phase is not
            (InstallerOperationPhase.Failed or InstallerOperationPhase.Cancelled))
        {
            return false;
        }

        // Older journals have no InterruptedPhase. The operation may
        // already have started mutating files, so never guess.
        return entry.InterruptedPhase is null ||
            IsMutationPhase(entry.InterruptedPhase.Value) ||
            IsRebootPhase(entry.InterruptedPhase.Value);
    }

    public static bool IsRebootPhase(InstallerOperationPhase phase)
        => phase is InstallerOperationPhase.AwaitingReboot or
            InstallerOperationPhase.RebootResumeArmed;

    private static bool IsMutationPhase(InstallerOperationPhase phase)
        => phase is InstallerOperationPhase.Applying or
            InstallerOperationPhase.ApplyingIntegrations or
            InstallerOperationPhase.SavingState or
            InstallerOperationPhase.RemovingState;
}
