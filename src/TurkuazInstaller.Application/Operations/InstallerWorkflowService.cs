// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerWorkflowService.cs
// 📌 Amac: Download, verification, staging, apply ve state kaydi adimlarini install/update/repair/rollback use-case'lerinde koordine eder
// 📌 Modul - Service CSharp
// Version: 0.7.1
// Aciklama: UI ve CLI'nin ayni gercek installer workflow servisini kullanmasini saglar
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
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

    private readonly IArtifactDownloader _artifactDownloader;
    private readonly IArtifactVerifier _artifactVerifier;
    private readonly IPackageEngine _packageEngine;
    private readonly IInstallStateRepository _stateRepository;

    public InstallerWorkflowService(
        IArtifactDownloader artifactDownloader,
        IArtifactVerifier artifactVerifier,
        IPackageEngine packageEngine,
        IInstallStateRepository stateRepository)
    {
        _artifactDownloader = artifactDownloader;
        _artifactVerifier = artifactVerifier;
        _packageEngine = packageEngine;
        _stateRepository = stateRepository;
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
            Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
            Array.Empty<string>());

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
            Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
            Array.Empty<string>());

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

        if (release.PackageId != currentState.PackageId
            || !release.Version.Equals(currentState.Version))
        {
            throw new InvalidOperationException(
                RepairReleaseMismatch);
        }

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

        if (currentRelease.PackageId != currentState.PackageId
            || !currentRelease.Version.Equals(currentState.Version))
        {
            throw new InvalidOperationException(
                CurrentReleaseMismatch);
        }

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

        var downloadedPath = await _artifactDownloader
            .DownloadAsync(
                release.Artifact,
                stagingDirectory,
                cancellationToken)
            .ConfigureAwait(false);

        Report(
            progress,
            InstallerProgressStage.Verifying,
            VerifyPercent);

        var verification = await _artifactVerifier
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
}
