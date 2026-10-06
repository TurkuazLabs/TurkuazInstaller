// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerWorkflowService.cs
// 📌 Amac: Download, prerequisite, verification, staging, package mutation, journal, log ve state adimlarini installer use-case'lerinde koordine eder
// 📌 Modul - Service CSharp
// Version: 1.7.0
// Aciklama: Prerequisite/version policy zincirine package-owned Windows integration reconcile ve safe uninstall cleanup ekler
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Integrations;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Prerequisites;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;

namespace TurkuazInstaller.Application.Operations;

public sealed class InstallerWorkflowService
{
    private const int DownloadPercent = 15;
    private const int VerifyPercent = 35;
    private const int StagePercent = 55;
    private const int ApplyPercent = 80;
    private const int SavePercent = 95;
    private const int CompletePercent = 100;

    private const string UpdatePackageMismatch =
        "Update release package id does not match installed state.";

    private const string UpdateVersionInvalid =
        "Update release must be newer than the installed version.";

    private const string RepairReleaseMismatch =
        "Repair release must match the installed package and version.";

    private const string CurrentReleaseMismatch =
        "Current release must match the installed package state.";

    private const string RollbackDisabled =
        "Rollback is disabled by the current release manifest.";

    private const string SignatureVerifierMissing =
        "Artifact signature is required but no signature verifier is configured.";

    private const string PrerequisiteProbeMissing =
        "Manifest prerequisites are declared but no prerequisite probe is configured.";

    private const string PrerequisiteInstallerMissing =
        "Prerequisite auto-install is declared but no prerequisite installer is configured.";

    private const string RebootResumeSchedulerMissing =
        "Prerequisite auto-install requires a reboot resume scheduler before execution.";

    private const string PrerequisiteInstallingEvent =
        "prerequisite.installing";

    private const string PrerequisiteInstalledEvent =
        "prerequisite.installed";

    private readonly IArtifactDownloader _artifactDownloader;
    private readonly IArtifactVerifier _artifactVerifier;
    private readonly IPackageEngine _packageEngine;
    private readonly IInstallStateRepository _stateRepository;
    private readonly IArtifactSignatureVerifier? _signatureVerifier;
    private readonly ISystemPrerequisiteProbe? _prerequisiteProbe;
    private readonly IInstallerOperationLock? _operationLock;
    private readonly IInstallerOperationJournalRepository? _operationJournal;
    private readonly IInstallerEventLogger? _eventLogger;
    private readonly IPrerequisiteInstaller? _prerequisiteInstaller;
    private readonly IRebootResumeScheduler? _rebootResumeScheduler;
    private readonly VersionUpdatePolicyService? _versionPolicyService;
    private readonly IWindowsIntegrationManager? _windowsIntegrationManager;

    public InstallerWorkflowService(
        IArtifactDownloader artifactDownloader,
        IArtifactVerifier artifactVerifier,
        IPackageEngine packageEngine,
        IInstallStateRepository stateRepository,
        IArtifactSignatureVerifier? signatureVerifier = null,
        ISystemPrerequisiteProbe? prerequisiteProbe = null,
        IInstallerOperationLock? operationLock = null,
        IInstallerOperationJournalRepository? operationJournal = null,
        IInstallerEventLogger? eventLogger = null,
        IPrerequisiteInstaller? prerequisiteInstaller = null,
        IRebootResumeScheduler? rebootResumeScheduler = null,
        IVersionUpdatePolicyRepository? versionPolicyRepository = null,
        IWindowsIntegrationManager? windowsIntegrationManager = null)
    {
        _artifactDownloader = artifactDownloader;
        _artifactVerifier = artifactVerifier;
        _packageEngine = packageEngine;
        _stateRepository = stateRepository;
        _signatureVerifier = signatureVerifier;
        _prerequisiteProbe = prerequisiteProbe;
        _operationLock = operationLock;
        _operationJournal = operationJournal;
        _eventLogger = eventLogger;
        _prerequisiteInstaller = prerequisiteInstaller;
        _rebootResumeScheduler = rebootResumeScheduler;

        _versionPolicyService =
            versionPolicyRepository is null
                ? null
                : new VersionUpdatePolicyService(
                    versionPolicyRepository);

        _windowsIntegrationManager =
            windowsIntegrationManager;
    }

    public Task InstallAsync(
        PackageRelease release,
        string targetPath,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken,
        InstallerOperationJournalEntry? resumeEntry = null)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        return ExecuteTrackedAsync(
            release.PackageId,
            InstallerOperationType.Install,
            release.Version.ToString(),
            targetPath,
            cancellationToken,
            resumeEntry,
            async operation =>
            {
                await ValidatePrerequisitesAsync(
                        release,
                        stagingDirectory,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                var stage =
                    await PrepareStageAsync(
                            release,
                            stagingDirectory,
                            progress,
                            operation,
                            cancellationToken)
                        .ConfigureAwait(false);

                await operation
                    .SetPhaseAsync(
                        InstallerOperationPhase.Applying,
                        "package.applying",
                        "Package apply started.",
                        cancellationToken)
                    .ConfigureAwait(false);

                Report(
                    progress,
                    InstallerProgressStage.Applying,
                    ApplyPercent);

                var plan =
                    new InstallPlan(
                        release,
                        targetPath,
                        release.Install.Prerequisites,
                        release.Install.PreservePaths);

                await _packageEngine
                    .ApplyAsync(
                        plan,
                        stage,
                        cancellationToken)
                    .ConfigureAwait(false);

                await SaveStateAsync(
                        release,
                        targetPath,
                        progress,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                await ApplyWindowsIntegrationsAsync(
                        release,
                        targetPath,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                ReportCompleted(progress);
            });
    }

    public async Task UpdateAsync(
        PackageRelease release,
        InstalledPackageState currentState,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken,
        InstallerOperationJournalEntry? resumeEntry = null)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(currentState);

        if (release.PackageId != currentState.PackageId)
        {
            throw new InvalidOperationException(
                UpdatePackageMismatch);
        }

        if (release.Version <= currentState.Version)
        {
            throw new InvalidOperationException(
                UpdateVersionInvalid);
        }

        await EnsureVersionPolicyAllowsUpdateAsync(
                release,
                cancellationToken)
            .ConfigureAwait(false);

        await ExecuteTrackedAsync(
                release.PackageId,
                InstallerOperationType.Update,
                release.Version.ToString(),
                currentState.TargetPath,
                cancellationToken,
                resumeEntry,
                async operation =>
                {
                    await ValidatePrerequisitesAsync(
                            release,
                            stagingDirectory,
                            operation,
                            cancellationToken)
                        .ConfigureAwait(false);

                    var stage =
                        await PrepareStageAsync(
                                release,
                                stagingDirectory,
                                progress,
                                operation,
                                cancellationToken)
                            .ConfigureAwait(false);

                    await operation
                        .SetPhaseAsync(
                            InstallerOperationPhase.Applying,
                            "package.updating",
                            "Package update apply started.",
                            cancellationToken)
                        .ConfigureAwait(false);

                    Report(
                        progress,
                        InstallerProgressStage.Applying,
                        ApplyPercent);

                    var plan =
                        new InstallPlan(
                            release,
                            currentState.TargetPath,
                            release.Install.Prerequisites,
                            release.Install.PreservePaths);

                    await _packageEngine
                        .ApplyAsync(
                            plan,
                            stage,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await SaveStateAsync(
                            release,
                            currentState.TargetPath,
                            progress,
                            operation,
                            cancellationToken)
                        .ConfigureAwait(false);

                    await ApplyWindowsIntegrationsAsync(
                            release,
                            currentState.TargetPath,
                            operation,
                            cancellationToken)
                        .ConfigureAwait(false);

                    ReportCompleted(progress);
                })
            .ConfigureAwait(false);
    }

    public Task RepairAsync(
        PackageRelease release,
        InstalledPackageState currentState,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken,
        InstallerOperationJournalEntry? resumeEntry = null)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(currentState);

        return ExecuteTrackedAsync(
            release.PackageId,
            InstallerOperationType.Repair,
            release.Version.ToString(),
            currentState.TargetPath,
            cancellationToken,
            resumeEntry,
            async operation =>
            {
                if (
                    release.PackageId != currentState.PackageId ||
                    !release.Version.Equals(
                        currentState.Version))
                {
                    throw new InvalidOperationException(
                        RepairReleaseMismatch);
                }

                await ValidatePrerequisitesAsync(
                        release,
                        stagingDirectory,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                var stage =
                    await PrepareStageAsync(
                            release,
                            stagingDirectory,
                            progress,
                            operation,
                            cancellationToken)
                        .ConfigureAwait(false);

                await operation
                    .SetPhaseAsync(
                        InstallerOperationPhase.Applying,
                        "package.repairing",
                        "Package repair apply started.",
                        cancellationToken)
                    .ConfigureAwait(false);

                Report(
                    progress,
                    InstallerProgressStage.Applying,
                    ApplyPercent);

                await _packageEngine
                    .RepairAsync(
                        new RepairPlan(
                            release,
                            currentState.TargetPath),
                        stage,
                        cancellationToken)
                    .ConfigureAwait(false);

                await ApplyWindowsIntegrationsAsync(
                        release,
                        currentState.TargetPath,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                ReportCompleted(progress);
            });
    }

    public Task RollbackAsync(
        PackageRelease currentRelease,
        PackageRelease previousRelease,
        InstalledPackageState currentState,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken,
        InstallerOperationJournalEntry? resumeEntry = null)
    {
        ArgumentNullException.ThrowIfNull(currentRelease);
        ArgumentNullException.ThrowIfNull(previousRelease);
        ArgumentNullException.ThrowIfNull(currentState);

        return ExecuteTrackedAsync(
            currentRelease.PackageId,
            InstallerOperationType.Rollback,
            previousRelease.Version.ToString(),
            currentState.TargetPath,
            cancellationToken,
            resumeEntry,
            async operation =>
            {
                if (
                    currentRelease.PackageId != currentState.PackageId ||
                    !currentRelease.Version.Equals(
                        currentState.Version))
                {
                    throw new InvalidOperationException(
                        CurrentReleaseMismatch);
                }

                if (!currentRelease.Rollback.Supported)
                {
                    throw new InvalidOperationException(
                        RollbackDisabled);
                }

                await ValidatePrerequisitesAsync(
                        previousRelease,
                        stagingDirectory,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                var stage =
                    await PrepareStageAsync(
                            previousRelease,
                            stagingDirectory,
                            progress,
                            operation,
                            cancellationToken)
                        .ConfigureAwait(false);

                await operation
                    .SetPhaseAsync(
                        InstallerOperationPhase.Applying,
                        "package.rolling_back",
                        "Package rollback apply started.",
                        cancellationToken)
                    .ConfigureAwait(false);

                Report(
                    progress,
                    InstallerProgressStage.Applying,
                    ApplyPercent);

                await _packageEngine
                    .RollbackAsync(
                        new RollbackPlan(
                            currentRelease,
                            previousRelease,
                            currentState.TargetPath),
                        stage,
                        cancellationToken)
                    .ConfigureAwait(false);

                await SaveStateAsync(
                        previousRelease,
                        currentState.TargetPath,
                        progress,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                await ApplyWindowsIntegrationsAsync(
                        previousRelease,
                        currentState.TargetPath,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                ReportCompleted(progress);
            });
    }

    public Task UninstallAsync(
        InstalledPackageState currentState,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        return ExecuteTrackedAsync(
            currentState.PackageId,
            InstallerOperationType.Uninstall,
            currentState.Version.ToString(),
            currentState.TargetPath,
            cancellationToken,
            null,
            async operation =>
            {
                await operation
                    .SetPhaseAsync(
                        InstallerOperationPhase.Applying,
                        "package.uninstalling",
                        "Package uninstall started.",
                        cancellationToken)
                    .ConfigureAwait(false);

                Report(
                    progress,
                    InstallerProgressStage.Uninstalling,
                    ApplyPercent);

                await _packageEngine
                    .UninstallAsync(
                        new UninstallPlan(
                            currentState.PackageId,
                            currentState.Version,
                            currentState.TargetPath),
                        cancellationToken)
                    .ConfigureAwait(false);

                await operation
                    .SetPhaseAsync(
                        InstallerOperationPhase.RemovingState,
                        "state.removing",
                        "Installed package state removal started.",
                        cancellationToken)
                    .ConfigureAwait(false);

                Report(
                    progress,
                    InstallerProgressStage.RemovingState,
                    SavePercent);

                await RemoveWindowsIntegrationsBestEffortAsync(
                        currentState.PackageId,
                        operation,
                        cancellationToken)
                    .ConfigureAwait(false);

                await _stateRepository
                    .DeleteAsync(
                        currentState.PackageId,
                        cancellationToken)
                    .ConfigureAwait(false);

                ReportCompleted(progress);
            });
    }

    private async Task EnsureVersionPolicyAllowsUpdateAsync(
        PackageRelease release,
        CancellationToken cancellationToken)
    {
        if (_versionPolicyService is null)
        {
            return;
        }

        var evaluation =
            await _versionPolicyService
                .EvaluateAsync(
                    release.PackageId,
                    release.Channel,
                    release.Version,
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            evaluation.Decision ==
            VersionUpdatePolicyDecision.Allowed)
        {
            return;
        }

        throw new InstallerVersionPolicyException(
            release.PackageId,
            release.Version,
            evaluation.Decision,
            evaluation.Policy?.MaximumVersion);
    }

    private async Task ExecuteTrackedAsync(
        PackageId packageId,
        InstallerOperationType operationType,
        string? version,
        string targetPath,
        CancellationToken cancellationToken,
        InstallerOperationJournalEntry? resumeEntry,
        Func<InstallerOperationContext, Task> operation)
    {
        await using var operationLease =
            await AcquireOperationLockAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        var context =
            resumeEntry is null
                ? await InstallerOperationContext
                    .StartAsync(
                        packageId,
                        operationType,
                        version,
                        targetPath,
                        _operationJournal,
                        _eventLogger,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await ResumeOperationContextAsync(
                        packageId,
                        operationType,
                        version,
                        targetPath,
                        resumeEntry,
                        cancellationToken)
                    .ConfigureAwait(false);

        try
        {
            await operation(context)
                .ConfigureAwait(false);

            await context
                .CompleteAsync(
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (InstallerRebootRequiredException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            await context
                .CancelAsync(
                    CancellationToken.None)
                .ConfigureAwait(false);

            throw;
        }
        catch (Exception exception)
        {
            await context
                .FailAsync(
                    exception,
                    CancellationToken.None)
                .ConfigureAwait(false);

            throw;
        }
    }

    private async Task<InstallerOperationContext> ResumeOperationContextAsync(
        PackageId packageId,
        InstallerOperationType operationType,
        string? version,
        string targetPath,
        InstallerOperationJournalEntry resumeEntry,
        CancellationToken cancellationToken)
    {
        if (
            resumeEntry.Phase is not
                InstallerOperationPhase.AwaitingReboot and not
                InstallerOperationPhase.RebootResumeArmed)
        {
            throw new InvalidOperationException(
                "Installer operation can only resume from a reboot-safe checkpoint phase.");
        }

        if (
            resumeEntry.PackageId != packageId ||
            resumeEntry.Operation != operationType ||
            !string.Equals(
                resumeEntry.Version,
                version,
                StringComparison.Ordinal) ||
            !string.Equals(
                resumeEntry.TargetPath,
                targetPath,
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(
                resumeEntry.PendingPrerequisiteId))
        {
            throw new InvalidOperationException(
                "Installer reboot resume journal does not match the requested operation.");
        }

        return await InstallerOperationContext
            .ResumeAsync(
                resumeEntry,
                _operationJournal,
                _eventLogger,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IAsyncDisposable> AcquireOperationLockAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        if (_operationLock is null)
        {
            return NoopAsyncDisposable.Instance;
        }

        return await _operationLock
            .AcquireAsync(
                packageId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PackageStage> PrepareStageAsync(
        PackageRelease release,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        InstallerOperationContext operation,
        CancellationToken cancellationToken)
    {
        await operation
            .SetPhaseAsync(
                InstallerOperationPhase.Downloading,
                "artifact.downloading",
                "Artifact download started.",
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Downloading,
            DownloadPercent);

        var downloadedPath =
            await _artifactDownloader
                .DownloadAsync(
                    release.Artifact,
                    stagingDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

        await operation
            .SetPhaseAsync(
                InstallerOperationPhase.Verifying,
                "artifact.verifying",
                "Artifact integrity and signature verification started.",
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Verifying,
            VerifyPercent);

        var verification =
            await _artifactVerifier
                .VerifyAsync(
                    downloadedPath,
                    release.Artifact,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!verification.IsValid)
        {
            throw new InvalidDataException(
                verification.Message);
        }

        await VerifySignatureAsync(
                release.Artifact,
                downloadedPath,
                cancellationToken)
            .ConfigureAwait(false);

        await operation
            .SetPhaseAsync(
                InstallerOperationPhase.Staging,
                "artifact.staging",
                "Verified artifact staging started.",
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Staging,
            StagePercent);

        return await _packageEngine
            .StageAsync(
                release,
                downloadedPath,
                stagingDirectory,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task VerifySignatureAsync(
        ArtifactDescriptor artifact,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        var signature =
            artifact.Signature;

        if (signature is null)
        {
            return;
        }

        if (_signatureVerifier is null)
        {
            throw new InvalidDataException(
                SignatureVerifierMissing);
        }

        var result =
            await _signatureVerifier
                .VerifyAsync(
                    artifactPath,
                    signature,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!result.IsValid)
        {
            throw new InvalidDataException(
                result.Message);
        }
    }

    private async Task ValidatePrerequisitesAsync(
        PackageRelease release,
        string stagingDirectory,
        InstallerOperationContext operation,
        CancellationToken cancellationToken)
    {
        var pendingPrerequisiteId =
            operation.PendingPrerequisiteId;

        if (
            release.Install.Prerequisites.Count == 0)
        {
            if (pendingPrerequisiteId is not null)
            {
                throw new InvalidOperationException(
                    "Pending reboot prerequisite is not present in the resumed signed manifest.");
            }

            await operation
                .SetPhaseAsync(
                    InstallerOperationPhase.ValidatingPrerequisites,
                    "prerequisite.validating",
                    "Prerequisite validation started.",
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (_prerequisiteProbe is null)
        {
            throw new InvalidOperationException(
                PrerequisiteProbeMissing);
        }

        string? validatedPendingPrerequisiteId = null;

        if (pendingPrerequisiteId is not null)
        {
            var pendingPrerequisite =
                release.Install.Prerequisites
                    .FirstOrDefault(
                        prerequisite =>
                            string.Equals(
                                prerequisite.Id,
                                pendingPrerequisiteId,
                                StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    "Pending reboot prerequisite is not present in the resumed signed manifest.");

            if (
                !_prerequisiteProbe.Supports(
                    pendingPrerequisite.Id))
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Unsupported prerequisite id: ",
                        pendingPrerequisite.Id));
            }

            var satisfiedAfterReboot =
                await _prerequisiteProbe
                    .IsSatisfiedAsync(
                        pendingPrerequisite,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!satisfiedAfterReboot)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Prerequisite is still not satisfied after reboot-safe resume: ",
                        pendingPrerequisite.Id,
                        " ",
                        pendingPrerequisite.VersionExpression));
            }

            validatedPendingPrerequisiteId =
                pendingPrerequisite.Id;

            await operation
                .ClearRebootCheckpointAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await operation
                .SetPhaseAsync(
                    InstallerOperationPhase.ValidatingPrerequisites,
                    "prerequisite.validating",
                    "Prerequisite validation started.",
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (
            var prerequisite in
            release.Install.Prerequisites)
        {
            if (
                validatedPendingPrerequisiteId is not null &&
                string.Equals(
                    prerequisite.Id,
                    validatedPendingPrerequisiteId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (
                !_prerequisiteProbe.Supports(
                    prerequisite.Id))
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Unsupported prerequisite id: ",
                        prerequisite.Id));
            }

            var satisfied =
                await _prerequisiteProbe
                    .IsSatisfiedAsync(
                        prerequisite,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (satisfied)
            {
                continue;
            }

            var installAction =
                prerequisite.InstallAction;

            if (installAction is null)
            {
                throw new InvalidOperationException(
                    BuildUnsatisfiedPrerequisiteMessage(
                        prerequisite));
            }

            if (_prerequisiteInstaller is null)
            {
                throw new InvalidOperationException(
                    PrerequisiteInstallerMissing);
            }

            if (_rebootResumeScheduler is null)
            {
                throw new InvalidOperationException(
                    RebootResumeSchedulerMissing);
            }

            await operation
                .SetPhaseAsync(
                    InstallerOperationPhase.ValidatingPrerequisites,
                    PrerequisiteInstallingEvent,
                    string.Concat(
                        "Prerequisite auto-install started: ",
                        prerequisite.Id,
                        "."),
                    cancellationToken)
                .ConfigureAwait(false);

            var installerPath =
                await _artifactDownloader
                    .DownloadAsync(
                        installAction.Artifact,
                        stagingDirectory,
                        cancellationToken)
                    .ConfigureAwait(false);

            var verification =
                await _artifactVerifier
                    .VerifyAsync(
                        installerPath,
                        installAction.Artifact,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!verification.IsValid)
            {
                throw new InvalidDataException(
                    verification.Message);
            }

            await VerifySignatureAsync(
                    installAction.Artifact,
                    installerPath,
                    cancellationToken)
                .ConfigureAwait(false);

            await _rebootResumeScheduler
                .ScheduleAsync(
                    release.PackageId,
                    cancellationToken)
                .ConfigureAwait(false);

            await operation
                .ArmRebootResumeAsync(
                    prerequisite.Id,
                    CancellationToken.None)
                .ConfigureAwait(false);

            var keepResumeScheduled = false;

            try
            {
                var installResult =
                    await _prerequisiteInstaller
                        .InstallAsync(
                            installerPath,
                            installAction,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (installResult.RequiresReboot)
                {
                    keepResumeScheduled = true;

                    var rebootMessage =
                        string.Concat(
                            "Prerequisite installed and reboot is required before re-probe: ",
                            prerequisite.Id,
                            ".");

                    await operation
                        .AwaitRebootAsync(
                            prerequisite.Id,
                            rebootMessage,
                            CancellationToken.None)
                        .ConfigureAwait(false);

                    throw new InstallerRebootRequiredException(
                        prerequisite.Id,
                        installResult.Disposition,
                        installResult.ExitCode);
                }
            }
            finally
            {
                if (!keepResumeScheduled)
                {
                    await _rebootResumeScheduler
                        .CancelAsync(
                            release.PackageId,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }

            var satisfiedAfterInstall =
                await _prerequisiteProbe
                    .IsSatisfiedAsync(
                        prerequisite,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!satisfiedAfterInstall)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Prerequisite auto-install completed but requirement is still not satisfied: ",
                        prerequisite.Id,
                        " ",
                        prerequisite.VersionExpression));
            }

            await operation
                .ClearRebootCheckpointAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            await operation
                .SetPhaseAsync(
                    InstallerOperationPhase.ValidatingPrerequisites,
                    PrerequisiteInstalledEvent,
                    string.Concat(
                        "Prerequisite auto-install completed: ",
                        prerequisite.Id,
                        "."),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static string BuildUnsatisfiedPrerequisiteMessage(
        Prerequisite prerequisite)
    {
        return string.Concat(
            "Required prerequisite is not satisfied: ",
            prerequisite.Id,
            " ",
            prerequisite.VersionExpression);
    }

    private async Task ApplyWindowsIntegrationsAsync(
        PackageRelease release,
        string targetPath,
        InstallerOperationContext operation,
        CancellationToken cancellationToken)
    {
        if (_windowsIntegrationManager is null)
        {
            if (release.Install.WindowsIntegration.HasActions)
            {
                throw new InvalidOperationException(
                    "Windows integration actions are declared but no integration manager is configured.");
            }

            return;
        }

        await operation
            .SetPhaseAsync(
                InstallerOperationPhase.ApplyingIntegrations,
                "integration.applying",
                "Package-owned Windows integrations are being reconciled.",
                cancellationToken)
            .ConfigureAwait(false);

        await _windowsIntegrationManager
            .ApplyAsync(
                release.PackageId,
                targetPath,
                release.Install.WindowsIntegration,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RemoveWindowsIntegrationsBestEffortAsync(
        PackageId packageId,
        InstallerOperationContext operation,
        CancellationToken cancellationToken)
    {
        if (_windowsIntegrationManager is null)
        {
            return;
        }

        await operation
            .SetPhaseAsync(
                InstallerOperationPhase.ApplyingIntegrations,
                "integration.removing",
                "Package-owned Windows integrations are being removed.",
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await _windowsIntegrationManager
                .RemoveAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await operation
                .WarnAsync(
                    "integration.cleanup_failed",
                    exception.Message,
                    exception.GetType().FullName,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private async Task SaveStateAsync(
        PackageRelease release,
        string targetPath,
        IProgress<InstallerOperationProgress>? progress,
        InstallerOperationContext operation,
        CancellationToken cancellationToken)
    {
        await operation
            .SetPhaseAsync(
                InstallerOperationPhase.SavingState,
                "state.saving",
                "Installed package state save started.",
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.SavingState,
            SavePercent);

        await _stateRepository
            .SaveAsync(
                new InstalledPackageState(
                    release.PackageId,
                    release.Version,
                    release.Channel,
                    targetPath),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ReportCompleted(
        IProgress<InstallerOperationProgress>? progress)
    {
        Report(
            progress,
            InstallerProgressStage.Completed,
            CompletePercent);
    }

    private static void Report(
        IProgress<InstallerOperationProgress>? progress,
        InstallerProgressStage stage,
        int percent)
    {
        progress?.Report(
            new InstallerOperationProgress(
                stage,
                percent));
    }

    private sealed class NoopAsyncDisposable
        : IAsyncDisposable
    {
        public static NoopAsyncDisposable Instance { get; } =
            new();

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
