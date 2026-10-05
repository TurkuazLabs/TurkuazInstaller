// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerWorkflowService.cs
// 📌 Amac: Download, prerequisite, verification, staging, package mutation ve state adimlarini installer use-case'lerinde koordine eder
// 📌 Modul - Service CSharp
// Version: 1.1.0
// Aciklama: Manifest policy, publisher-pinned Authenticode, package operation lock, preserve paths ve uninstall state davranisini gercek runtime akisina baglar
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Plans;
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

    private readonly IArtifactDownloader _artifactDownloader;
    private readonly IArtifactVerifier _artifactVerifier;
    private readonly IPackageEngine _packageEngine;
    private readonly IInstallStateRepository _stateRepository;
    private readonly IArtifactSignatureVerifier? _signatureVerifier;
    private readonly ISystemPrerequisiteProbe? _prerequisiteProbe;
    private readonly IInstallerOperationLock? _operationLock;

    public InstallerWorkflowService(
        IArtifactDownloader artifactDownloader,
        IArtifactVerifier artifactVerifier,
        IPackageEngine packageEngine,
        IInstallStateRepository stateRepository,
        IArtifactSignatureVerifier? signatureVerifier = null,
        ISystemPrerequisiteProbe? prerequisiteProbe = null,
        IInstallerOperationLock? operationLock = null)
    {
        _artifactDownloader = artifactDownloader;
        _artifactVerifier = artifactVerifier;
        _packageEngine = packageEngine;
        _stateRepository = stateRepository;
        _signatureVerifier = signatureVerifier;
        _prerequisiteProbe = prerequisiteProbe;
        _operationLock = operationLock;
    }

    public async Task InstallAsync(
        PackageRelease release,
        string targetPath,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        await using var operationLease =
            await AcquireOperationLockAsync(
                    release.PackageId,
                    cancellationToken)
                .ConfigureAwait(false);

        await ValidatePrerequisitesAsync(
                release,
                cancellationToken)
            .ConfigureAwait(false);

        var stage = await PrepareStageAsync(
                release,
                stagingDirectory,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Applying,
            ApplyPercent);

        var plan = new InstallPlan(
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
                cancellationToken)
            .ConfigureAwait(false);

        ReportCompleted(progress);
    }

    public async Task UpdateAsync(
        PackageRelease release,
        InstalledPackageState currentState,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(currentState);

        await using var operationLease =
            await AcquireOperationLockAsync(
                    release.PackageId,
                    cancellationToken)
                .ConfigureAwait(false);

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

        await ValidatePrerequisitesAsync(
                release,
                cancellationToken)
            .ConfigureAwait(false);

        var stage = await PrepareStageAsync(
                release,
                stagingDirectory,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Applying,
            ApplyPercent);

        var plan = new InstallPlan(
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
                cancellationToken)
            .ConfigureAwait(false);

        ReportCompleted(progress);
    }

    public async Task RepairAsync(
        PackageRelease release,
        InstalledPackageState currentState,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(currentState);

        await using var operationLease =
            await AcquireOperationLockAsync(
                    release.PackageId,
                    cancellationToken)
                .ConfigureAwait(false);

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
                cancellationToken)
            .ConfigureAwait(false);

        var stage = await PrepareStageAsync(
                release,
                stagingDirectory,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Applying,
            ApplyPercent);

        var plan = new RepairPlan(
            release,
            currentState.TargetPath);

        await _packageEngine
            .RepairAsync(
                plan,
                stage,
                cancellationToken)
            .ConfigureAwait(false);

        ReportCompleted(progress);
    }

    public async Task RollbackAsync(
        PackageRelease currentRelease,
        PackageRelease previousRelease,
        InstalledPackageState currentState,
        string stagingDirectory,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentRelease);
        ArgumentNullException.ThrowIfNull(previousRelease);
        ArgumentNullException.ThrowIfNull(currentState);

        await using var operationLease =
            await AcquireOperationLockAsync(
                    currentRelease.PackageId,
                    cancellationToken)
                .ConfigureAwait(false);

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
                cancellationToken)
            .ConfigureAwait(false);

        var stage = await PrepareStageAsync(
                previousRelease,
                stagingDirectory,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Applying,
            ApplyPercent);

        var plan = new RollbackPlan(
            currentRelease,
            previousRelease,
            currentState.TargetPath);

        await _packageEngine
            .RollbackAsync(
                plan,
                stage,
                cancellationToken)
            .ConfigureAwait(false);

        await SaveStateAsync(
                previousRelease,
                currentState.TargetPath,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        ReportCompleted(progress);
    }

    public async Task UninstallAsync(
        InstalledPackageState currentState,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentState);

        await using var operationLease =
            await AcquireOperationLockAsync(
                    currentState.PackageId,
                    cancellationToken)
                .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Uninstalling,
            ApplyPercent);

        var plan = new UninstallPlan(
            currentState.PackageId,
            currentState.Version,
            currentState.TargetPath);

        await _packageEngine
            .UninstallAsync(
                plan,
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.RemovingState,
            SavePercent);

        await _stateRepository
            .DeleteAsync(
                currentState.PackageId,
                cancellationToken)
            .ConfigureAwait(false);

        ReportCompleted(progress);
    }

    private async Task<IAsyncDisposable> AcquireOperationLockAsync(
        TurkuazInstaller.Domain.Products.PackageId packageId,
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
        CancellationToken cancellationToken)
    {
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
                release,
                downloadedPath,
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
        PackageRelease release,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        var signature =
            release.Artifact.Signature;

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
        CancellationToken cancellationToken)
    {
        if (
            release.Install.Prerequisites.Count == 0)
        {
            return;
        }

        if (_prerequisiteProbe is null)
        {
            throw new InvalidOperationException(
                PrerequisiteProbeMissing);
        }

        foreach (
            var prerequisite in
            release.Install.Prerequisites)
        {
            var satisfied =
                await _prerequisiteProbe
                    .IsSatisfiedAsync(
                        prerequisite,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!satisfied)
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Required prerequisite is not satisfied: ",
                        prerequisite.Id,
                        " ",
                        prerequisite.VersionExpression));
            }
        }
    }

    private async Task SaveStateAsync(
        PackageRelease release,
        string targetPath,
        IProgress<InstallerOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
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
