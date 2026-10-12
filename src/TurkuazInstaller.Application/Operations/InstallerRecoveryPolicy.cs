// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerRecoveryPolicy.cs
// 📌 Amac: Kesintiye ugrayan operasyonlarin recovery kilidini tek typed kuralla belirler
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Workflow mutation guard ve salt-okunur recovery inceleme ayni policy'yi kullanir
// Bagimli Oldugu Katman: Service | Domain

using TurkuazInstaller.Domain.Operations;

namespace TurkuazInstaller.Application.Operations;

internal static class InstallerRecoveryPolicy
{
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
