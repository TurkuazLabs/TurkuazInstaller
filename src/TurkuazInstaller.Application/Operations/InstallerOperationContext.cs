// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerOperationContext.cs
// 📌 Amac: Bir installer operasyonunun crash journal ve structured log checkpoint yasam dongusunu koordine eder
// 📌 Modul - Service CSharp
// Version: 1.4.0
// Aciklama: Diagnostics storage hatalarini izole eder; pre-execution resume arm, reboot wait, resume ve clear checkpointlerini yonetir
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Application.Operations;

internal sealed class InstallerOperationContext
{
    private readonly IInstallerOperationJournalRepository? _journal;
    private readonly IInstallerEventLogger? _logger;
    private InstallerOperationJournalEntry _entry;

    private InstallerOperationContext(
        InstallerOperationJournalEntry entry,
        IInstallerOperationJournalRepository? journal,
        IInstallerEventLogger? logger)
    {
        _entry = entry;
        _journal = journal;
        _logger = logger;
    }

    public string? PendingPrerequisiteId =>
        _entry.PendingPrerequisiteId;

    public static async Task<InstallerOperationContext> StartAsync(
        PackageId packageId,
        InstallerOperationType operation,
        string? version,
        string targetPath,
        IInstallerOperationJournalRepository? journal,
        IInstallerEventLogger? logger,
        CancellationToken cancellationToken)
    {
        var now =
            DateTimeOffset.UtcNow;

        var context =
            new InstallerOperationContext(
                new InstallerOperationJournalEntry(
                    Guid.NewGuid(),
                    packageId,
                    operation,
                    version,
                    targetPath,
                    InstallerOperationPhase.Started,
                    now,
                    now,
                    null),
                journal,
                logger);

        await context
            .PersistBestEffortAsync(
                InstallerEventLevel.Information,
                "operation.started",
                "Installer operation started.",
                null,
                cancellationToken)
            .ConfigureAwait(false);

        return context;
    }

    public static async Task<InstallerOperationContext> ResumeAsync(
        InstallerOperationJournalEntry entry,
        IInstallerOperationJournalRepository? journal,
        IInstallerEventLogger? logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            entry);

        var context =
            new InstallerOperationContext(
                entry,
                journal,
                logger);

        await context
            .PersistBestEffortAsync(
                InstallerEventLevel.Information,
                "operation.resumed",
                "Installer operation resumed after reboot.",
                null,
                cancellationToken)
            .ConfigureAwait(false);

        return context;
    }

    public async Task SetPhaseAsync(
        InstallerOperationPhase phase,
        string eventName,
        string message,
        CancellationToken cancellationToken)
    {
        _entry =
            _entry with
            {
                Phase = phase,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Failure = null
            };

        await PersistBestEffortAsync(
                InstallerEventLevel.Information,
                eventName,
                message,
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ArmRebootResumeAsync(
        string prerequisiteId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            prerequisiteId);

        _entry =
            _entry with
            {
                Phase = InstallerOperationPhase.RebootResumeArmed,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Failure = null,
                PendingPrerequisiteId =
                    prerequisiteId.Trim()
            };

        await PersistBestEffortAsync(
                InstallerEventLevel.Information,
                "operation.reboot_resume_armed",
                "Prerequisite execution armed for reboot-safe resume.",
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AwaitRebootAsync(
        string prerequisiteId,
        string message,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            prerequisiteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message);

        _entry =
            _entry with
            {
                Phase = InstallerOperationPhase.AwaitingReboot,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Failure = null,
                PendingPrerequisiteId =
                    prerequisiteId.Trim()
            };

        await PersistBestEffortAsync(
                InstallerEventLevel.Warning,
                "operation.awaiting_reboot",
                message,
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ClearRebootCheckpointAsync(
        CancellationToken cancellationToken)
    {
        if (_entry.PendingPrerequisiteId is null)
        {
            return;
        }

        _entry =
            _entry with
            {
                Phase = InstallerOperationPhase.ValidatingPrerequisites,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Failure = null,
                PendingPrerequisiteId = null
            };

        await PersistBestEffortAsync(
                InstallerEventLevel.Information,
                "operation.reboot_checkpoint_cleared",
                "Post-reboot prerequisite validation completed.",
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CompleteAsync(
        CancellationToken cancellationToken)
    {
        _entry =
            _entry with
            {
                Phase = InstallerOperationPhase.Completed,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Failure = null
            };

        await PersistBestEffortAsync(
                InstallerEventLevel.Information,
                "operation.completed",
                "Installer operation completed.",
                null,
                cancellationToken)
            .ConfigureAwait(false);

        if (_journal is null)
        {
            return;
        }

        try
        {
            await _journal
                .DeleteAsync(
                    _entry.PackageId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
        }
    }

    public Task CancelAsync(
        CancellationToken cancellationToken)
    {
        return FailOrCancelAsync(
            InstallerOperationPhase.Cancelled,
            InstallerEventLevel.Warning,
            "operation.cancelled",
            "Installer operation was cancelled.",
            null,
            cancellationToken);
    }

    public Task FailAsync(
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return FailOrCancelAsync(
            InstallerOperationPhase.Failed,
            InstallerEventLevel.Error,
            "operation.failed",
            exception.Message,
            exception.GetType().FullName,
            cancellationToken);
    }

    private async Task FailOrCancelAsync(
        InstallerOperationPhase phase,
        InstallerEventLevel level,
        string eventName,
        string message,
        string? errorType,
        CancellationToken cancellationToken)
    {
        _entry =
            _entry with
            {
                Phase = phase,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Failure = message
            };

        await PersistBestEffortAsync(
                level,
                eventName,
                message,
                errorType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PersistBestEffortAsync(
        InstallerEventLevel level,
        string eventName,
        string message,
        string? errorType,
        CancellationToken cancellationToken)
    {
        if (_journal is not null)
        {
            try
            {
                await _journal
                    .SaveAsync(
                        _entry,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
            }
        }

        if (_logger is not null)
        {
            try
            {
                await _logger
                    .WriteAsync(
                        new InstallerEventEntry(
                            DateTimeOffset.UtcNow,
                            _entry.OperationId,
                            _entry.PackageId,
                            _entry.Operation,
                            _entry.Phase,
                            level,
                            eventName,
                            message,
                            errorType),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
