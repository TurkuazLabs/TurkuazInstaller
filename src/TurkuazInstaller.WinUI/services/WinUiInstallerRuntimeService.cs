// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/services/WinUiInstallerRuntimeService.cs
// 📌 Amac: Presentation requestlerini signed provider, resumable workflow, repository ve Velopack runtime operasyonlarina baglar
// 📌 Modul - Service CSharp
// Version: 1.5.0
// Aciklama: Signed provider uzerinden version-policy-aware discovery, manual mutation ve reboot-resume akislarini koordine eder
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.Services;
using TurkuazInstaller.Presentation.ViewModels;
using TurkuazInstaller.WinUI.Config;
using TurkuazInstaller.WinUI.Tools;

namespace TurkuazInstaller.WinUI.Services;

internal sealed class WinUiInstallerRuntimeService
    : IInstallerRuntimeService
{
    private const string ReleaseNotFoundMessage =
        "Manifest release could not be resolved.";

    private const string RollbackSourceRequiredMessage =
        "Rollback manifest source is required.";

    private const string RollbackReleaseNotFoundMessage =
        "Rollback release could not be resolved.";

    private const string InstalledStateNotFoundMessage =
        "Installed package state was not found.";

    private const string ResumeRequestNotFoundMessage =
        "Persisted reboot resume request was not found.";

    private const string ResumeJournalNotFoundMessage =
        "AwaitingReboot operation journal was not found.";

    private const string PendingRebootMessage =
        "This package has an operation waiting for Windows reboot and must resume before another mutation can start.";

    private readonly ManifestReleaseProviderFactory _providerFactory;
    private readonly InstallerWorkflowService _workflowService;
    private readonly IInstallStateRepository _stateRepository;
    private readonly IInstallerOperationJournalRepository _operationJournal;
    private readonly IInstallerResumeRequestRepository _resumeRequestRepository;
    private readonly IVersionUpdatePolicyRepository? _versionPolicyRepository;
    private readonly DesktopRuntimeOptions _options;

    public WinUiInstallerRuntimeService(
        ManifestReleaseProviderFactory providerFactory,
        InstallerWorkflowService workflowService,
        IInstallStateRepository stateRepository,
        IInstallerOperationJournalRepository operationJournal,
        IInstallerResumeRequestRepository resumeRequestRepository,
        DesktopRuntimeOptions options,
        IVersionUpdatePolicyRepository? versionPolicyRepository = null)
    {
        _providerFactory = providerFactory;
        _workflowService = workflowService;
        _stateRepository = stateRepository;
        _operationJournal = operationJournal;
        _resumeRequestRepository = resumeRequestRepository;
        _options = options;
        _versionPolicyRepository = versionPolicyRepository;
    }

    public async Task<UpdateCheckResult> CheckUpdateAsync(
        InstallerUpdateCheckRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var packageId =
            PackageId.Parse(
                request.PackageId);

        var provider =
            _providerFactory.Create(
                request.ManifestSource);

        return await new UpdateCheckService(
                provider,
                _stateRepository,
                _versionPolicyRepository)
            .ExecuteAsync(
                packageId,
                request.Channel,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task ExecuteAsync(
        InstallerDesktopRequest request,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);
        ArgumentNullException.ThrowIfNull(
            progress);

        var packageId =
            PackageId.Parse(
                request.PackageId);

        await EnsureNoPendingRebootAsync(
                packageId,
                cancellationToken)
            .ConfigureAwait(false);

        await ExecuteInternalAsync(
                request,
                progress,
                cancellationToken,
                null,
                null)
            .ConfigureAwait(false);
    }

    public async Task ResumeAsync(
        PackageId packageId,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        ArgumentNullException.ThrowIfNull(
            progress);

        var resumeRequest =
            await _resumeRequestRepository
                .GetAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                ResumeRequestNotFoundMessage);

        var journal =
            await _operationJournal
                .GetAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                ResumeJournalNotFoundMessage);

        ValidateResumeCheckpoint(
            packageId,
            resumeRequest,
            journal);

        var desktopRequest =
            new InstallerDesktopRequest(
                MapOperationKind(
                    resumeRequest.Operation),
                packageId.Value,
                resumeRequest.Channel,
                resumeRequest.ManifestSource,
                resumeRequest.RollbackManifestSource,
                journal.TargetPath);

        await ExecuteInternalAsync(
                desktopRequest,
                progress,
                cancellationToken,
                journal,
                resumeRequest)
            .ConfigureAwait(false);
    }

    private async Task ExecuteInternalAsync(
        InstallerDesktopRequest request,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken,
        InstallerOperationJournalEntry? resumeEntry,
        InstallerResumeRequest? expectedResumeRequest)
    {
        var packageId =
            PackageId.Parse(
                request.PackageId);

        if (
            request.Operation ==
            InstallerOperationKind.Uninstall)
        {
            if (resumeEntry is not null)
            {
                throw new InvalidOperationException(
                    "Uninstall cannot resume from a prerequisite reboot checkpoint.");
            }

            await _workflowService
                .UninstallAsync(
                    await RequireInstalledStateAsync(
                            packageId,
                            cancellationToken)
                        .ConfigureAwait(false),
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        var provider =
            _providerFactory.Create(
                request.ManifestSource);

        var release =
            await provider
                .GetLatestReleaseAsync(
                    packageId,
                    request.Channel,
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                ReleaseNotFoundMessage);

        var operationStagingRoot =
            CreateOperationStagingRoot();

        try
        {
            switch (request.Operation)
            {
                case InstallerOperationKind.Install:
                    ValidateExpectedResumeRelease(
                        expectedResumeRequest,
                        release);

                    await ExecuteResumableAsync(
                            CreateResumeRequest(
                                request,
                                InstallerOperationType.Install,
                                release),
                            () =>
                                _workflowService.InstallAsync(
                                    release,
                                    ResolveInstallTarget(
                                        request.TargetPath,
                                        release),
                                    operationStagingRoot,
                                    progress,
                                    cancellationToken,
                                    resumeEntry),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case InstallerOperationKind.Update:
                {
                    ValidateExpectedResumeRelease(
                        expectedResumeRequest,
                        release);

                    var state =
                        await RequireInstalledStateAsync(
                                packageId,
                                cancellationToken)
                            .ConfigureAwait(false);

                    await ExecuteResumableAsync(
                            CreateResumeRequest(
                                request,
                                InstallerOperationType.Update,
                                release),
                            () =>
                                _workflowService.UpdateAsync(
                                    release,
                                    state,
                                    operationStagingRoot,
                                    progress,
                                    cancellationToken,
                                    resumeEntry),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                case InstallerOperationKind.Repair:
                {
                    ValidateExpectedResumeRelease(
                        expectedResumeRequest,
                        release);

                    var state =
                        await RequireInstalledStateAsync(
                                packageId,
                                cancellationToken)
                            .ConfigureAwait(false);

                    await ExecuteResumableAsync(
                            CreateResumeRequest(
                                request,
                                InstallerOperationType.Repair,
                                release),
                            () =>
                                _workflowService.RepairAsync(
                                    release,
                                    state,
                                    operationStagingRoot,
                                    progress,
                                    cancellationToken,
                                    resumeEntry),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                case InstallerOperationKind.Rollback:
                    await ExecuteRollbackAsync(
                            request,
                            packageId,
                            release,
                            operationStagingRoot,
                            progress,
                            cancellationToken,
                            resumeEntry,
                            expectedResumeRequest)
                        .ConfigureAwait(false);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(request.Operation));
            }
        }
        finally
        {
            TryDeleteDirectory(
                operationStagingRoot);
        }
    }

    private async Task ExecuteRollbackAsync(
        InstallerDesktopRequest request,
        PackageId packageId,
        PackageRelease currentRelease,
        string operationStagingRoot,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken,
        InstallerOperationJournalEntry? resumeEntry,
        InstallerResumeRequest? expectedResumeRequest)
    {
        if (request.RollbackManifestSource is null)
        {
            throw new InvalidOperationException(
                RollbackSourceRequiredMessage);
        }

        var previousProvider =
            _providerFactory.Create(
                request.RollbackManifestSource);

        var previousRelease =
            await previousProvider
                .GetLatestReleaseAsync(
                    packageId,
                    request.Channel,
                    cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                RollbackReleaseNotFoundMessage);

        ValidateExpectedResumeRelease(
            expectedResumeRequest,
            previousRelease);

        var state =
            await RequireInstalledStateAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        await ExecuteResumableAsync(
                CreateResumeRequest(
                    request,
                    InstallerOperationType.Rollback,
                    previousRelease),
                () =>
                    _workflowService.RollbackAsync(
                        currentRelease,
                        previousRelease,
                        state,
                        operationStagingRoot,
                        progress,
                        cancellationToken,
                        resumeEntry),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ExecuteResumableAsync(
        InstallerResumeRequest resumeRequest,
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        await _resumeRequestRepository
            .SaveAsync(
                resumeRequest,
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await operation()
                .ConfigureAwait(false);
        }
        catch (InstallerRebootRequiredException)
        {
            throw;
        }
        catch
        {
            await DeleteResumeRequestBestEffortAsync(
                    resumeRequest.PackageId)
                .ConfigureAwait(false);
            throw;
        }

        await DeleteResumeRequestBestEffortAsync(
                resumeRequest.PackageId)
            .ConfigureAwait(false);
    }

    private async Task EnsureNoPendingRebootAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        var journal =
            await _operationJournal
                .GetAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            journal?.Phase is
                InstallerOperationPhase.AwaitingReboot or
                InstallerOperationPhase.RebootResumeArmed)
        {
            throw new InvalidOperationException(
                PendingRebootMessage);
        }
    }

    private static void ValidateResumeCheckpoint(
        PackageId requestedPackageId,
        InstallerResumeRequest resumeRequest,
        InstallerOperationJournalEntry journal)
    {
        if (
            resumeRequest.PackageId != requestedPackageId ||
            journal.PackageId != requestedPackageId ||
            resumeRequest.Operation != journal.Operation ||
            journal.Phase is not
                InstallerOperationPhase.AwaitingReboot and not
                InstallerOperationPhase.RebootResumeArmed ||
            string.IsNullOrWhiteSpace(
                journal.PendingPrerequisiteId) ||
            !string.Equals(
                journal.Version,
                resumeRequest.ExpectedVersion.ToString(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Persisted reboot resume request does not match the operation journal.");
        }
    }

    private static void ValidateExpectedResumeRelease(
        InstallerResumeRequest? expectedResumeRequest,
        PackageRelease resolvedRelease)
    {
        if (expectedResumeRequest is null)
        {
            return;
        }

        if (
            !resolvedRelease.Version.Equals(
                expectedResumeRequest.ExpectedVersion) ||
            !string.Equals(
                resolvedRelease.Artifact.Digest.Sha256,
                expectedResumeRequest.ExpectedArtifactDigest.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Signed manifest release changed while the installer was waiting for reboot.");
        }
    }

    private static InstallerResumeRequest CreateResumeRequest(
        InstallerDesktopRequest request,
        InstallerOperationType operation,
        PackageRelease expectedRelease)
    {
        return new InstallerResumeRequest(
            expectedRelease.PackageId,
            operation,
            expectedRelease.Version,
            expectedRelease.Artifact.Digest,
            request.Channel,
            request.ManifestSource,
            request.RollbackManifestSource,
            DateTimeOffset.UtcNow);
    }

    private async Task DeleteResumeRequestBestEffortAsync(
        PackageId packageId)
    {
        try
        {
            await _resumeRequestRepository
                .DeleteAsync(
                    packageId,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task<InstalledPackageState>
        RequireInstalledStateAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
    {
        return await _stateRepository
            .GetAsync(
                packageId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                InstalledStateNotFoundMessage);
    }

    private static string ResolveInstallTarget(
        string requestedTarget,
        PackageRelease release)
    {
        var candidate =
            string.IsNullOrWhiteSpace(
                requestedTarget)
                ? release.Install.DefaultTargetPath
                : requestedTarget;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            throw new InvalidOperationException(
                InstallerUiLabels.TargetPathUnavailable);
        }

        return Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(
                candidate));
    }

    private string CreateOperationStagingRoot()
    {
        var path =
            Path.Combine(
                _options.StagingRoot,
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            path);

        return path;
    }

    private static InstallerOperationKind MapOperationKind(
        InstallerOperationType operation)
    {
        return operation switch
        {
            InstallerOperationType.Install =>
                InstallerOperationKind.Install,
            InstallerOperationType.Update =>
                InstallerOperationKind.Update,
            InstallerOperationType.Repair =>
                InstallerOperationKind.Repair,
            InstallerOperationType.Rollback =>
                InstallerOperationKind.Rollback,
            _ =>
                throw new InvalidOperationException(
                    "Persisted reboot resume operation is not supported.")
        };
    }

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(
                    path,
                    recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
