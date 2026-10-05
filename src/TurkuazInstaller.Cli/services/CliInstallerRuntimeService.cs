// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/services/CliInstallerRuntimeService.cs
// 📌 Amac: CLI invocationlarini signed provider, resumable workflow, repository ve Velopack runtime operasyonlarina baglar
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Silent install/update/repair/rollback/uninstall ve reboot resume akislarini WinUI'dan bagimsiz koordine eder
//
// Bagimli Oldugu Katman: Service | Repo | Tool | Config

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Cli.Config;
using TurkuazInstaller.Cli.Tools;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;

namespace TurkuazInstaller.Cli.Services;

internal sealed class CliInstallerRuntimeService
    : ICliInstallerRuntimeService
{
    private const string ReleaseNotFoundMessage =
        "Manifest release could not be resolved.";

    private const string InstalledStateNotFoundMessage =
        "Installed package state was not found.";

    private const string ResumeRequestNotFoundMessage =
        "Persisted reboot resume request was not found.";

    private const string ResumeJournalNotFoundMessage =
        "AwaitingReboot operation journal was not found.";

    private const string PendingRebootMessage =
        "Package has an operation waiting for reboot resume.";

    private readonly CliManifestReleaseProviderFactory _providerFactory;
    private readonly InstallerWorkflowService _workflowService;
    private readonly IInstallStateRepository _stateRepository;
    private readonly IInstallerOperationJournalRepository _operationJournal;
    private readonly IInstallerResumeRequestRepository _resumeRepository;
    private readonly CliRuntimeOptions _options;

    public CliInstallerRuntimeService(
        CliManifestReleaseProviderFactory providerFactory,
        InstallerWorkflowService workflowService,
        IInstallStateRepository stateRepository,
        IInstallerOperationJournalRepository operationJournal,
        IInstallerResumeRequestRepository resumeRepository,
        CliRuntimeOptions options)
    {
        _providerFactory = providerFactory;
        _workflowService = workflowService;
        _stateRepository = stateRepository;
        _operationJournal = operationJournal;
        _resumeRepository = resumeRepository;
        _options = options;
    }

    public async Task ExecuteAsync(
        CliInvocation invocation,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);
        ArgumentNullException.ThrowIfNull(progress);

        if (invocation.IsResume)
        {
            throw new InvalidOperationException(
                "Resume invocation must use ResumeAsync.");
        }

        await EnsureNoPendingRebootAsync(
                invocation.PackageId,
                cancellationToken)
            .ConfigureAwait(false);

        if (invocation.Operation == InstallerOperationType.Uninstall)
        {
            await _workflowService
                .UninstallAsync(
                    await RequireInstalledStateAsync(
                            invocation.PackageId,
                            cancellationToken)
                        .ConfigureAwait(false),
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        if (
            invocation.Operation is null ||
            string.IsNullOrWhiteSpace(
                invocation.ManifestSource))
        {
            throw new InvalidOperationException(
                "CLI mutation invocation is incomplete.");
        }

        var release =
            await ResolveReleaseAsync(
                    invocation.ManifestSource,
                    invocation.PackageId,
                    invocation.Channel,
                    cancellationToken)
                .ConfigureAwait(false);

        var stagingRoot =
            CreateOperationStagingRoot();

        try
        {
            switch (invocation.Operation.Value)
            {
                case InstallerOperationType.Install:
                    await ExecuteResumableAsync(
                            CreateResumeRequest(
                                invocation,
                                release),
                            () =>
                                _workflowService.InstallAsync(
                                    release,
                                    ResolveInstallTarget(
                                        invocation.TargetPath,
                                        release),
                                    stagingRoot,
                                    progress,
                                    cancellationToken),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case InstallerOperationType.Update:
                {
                    var state =
                        await RequireInstalledStateAsync(
                                invocation.PackageId,
                                cancellationToken)
                            .ConfigureAwait(false);

                    await ExecuteResumableAsync(
                            CreateResumeRequest(
                                invocation,
                                release),
                            () =>
                                _workflowService.UpdateAsync(
                                    release,
                                    state,
                                    stagingRoot,
                                    progress,
                                    cancellationToken),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                case InstallerOperationType.Repair:
                {
                    var state =
                        await RequireInstalledStateAsync(
                                invocation.PackageId,
                                cancellationToken)
                            .ConfigureAwait(false);

                    await ExecuteResumableAsync(
                            CreateResumeRequest(
                                invocation,
                                release),
                            () =>
                                _workflowService.RepairAsync(
                                    release,
                                    state,
                                    stagingRoot,
                                    progress,
                                    cancellationToken),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                case InstallerOperationType.Rollback:
                    await ExecuteRollbackAsync(
                            invocation,
                            release,
                            stagingRoot,
                            progress,
                            cancellationToken,
                            null,
                            null)
                        .ConfigureAwait(false);
                    break;

                default:
                    throw new InvalidOperationException(
                        "Unsupported CLI mutation operation.");
            }
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    public async Task ResumeAsync(
        PackageId packageId,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(progress);

        var resumeRequest =
            await _resumeRepository
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

        var release =
            await ResolveReleaseAsync(
                    resumeRequest.ManifestSource,
                    packageId,
                    resumeRequest.Channel,
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateExpectedResumeRelease(
            resumeRequest,
            release);

        var stagingRoot =
            CreateOperationStagingRoot();

        try
        {
            switch (resumeRequest.Operation)
            {
                case InstallerOperationType.Install:
                    await ExecuteResumableAsync(
                            resumeRequest,
                            () =>
                                _workflowService.InstallAsync(
                                    release,
                                    journal.TargetPath,
                                    stagingRoot,
                                    progress,
                                    cancellationToken,
                                    journal),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case InstallerOperationType.Update:
                {
                    var state =
                        await RequireInstalledStateAsync(
                                packageId,
                                cancellationToken)
                            .ConfigureAwait(false);

                    await ExecuteResumableAsync(
                            resumeRequest,
                            () =>
                                _workflowService.UpdateAsync(
                                    release,
                                    state,
                                    stagingRoot,
                                    progress,
                                    cancellationToken,
                                    journal),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                case InstallerOperationType.Repair:
                {
                    var state =
                        await RequireInstalledStateAsync(
                                packageId,
                                cancellationToken)
                            .ConfigureAwait(false);

                    await ExecuteResumableAsync(
                            resumeRequest,
                            () =>
                                _workflowService.RepairAsync(
                                    release,
                                    state,
                                    stagingRoot,
                                    progress,
                                    cancellationToken,
                                    journal),
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

                case InstallerOperationType.Rollback:
                {
                    var invocation =
                        new CliInvocation(
                            InstallerOperationType.Rollback,
                            packageId,
                            resumeRequest.Channel,
                            resumeRequest.ManifestSource,
                            resumeRequest.RollbackManifestSource,
                            journal.TargetPath,
                            true,
                            false);

                    await ExecuteRollbackAsync(
                            invocation,
                            release,
                            stagingRoot,
                            progress,
                            cancellationToken,
                            journal,
                            resumeRequest)
                        .ConfigureAwait(false);
                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "Persisted CLI reboot resume operation is not supported.");
            }
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task ExecuteRollbackAsync(
        CliInvocation invocation,
        PackageRelease currentRelease,
        string stagingRoot,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken,
        InstallerOperationJournalEntry? resumeEntry,
        InstallerResumeRequest? expectedResumeRequest)
    {
        if (string.IsNullOrWhiteSpace(
                invocation.RollbackManifestSource))
        {
            throw new InvalidOperationException(
                "Rollback manifest source is required.");
        }

        var previousRelease =
            await ResolveReleaseAsync(
                    invocation.RollbackManifestSource,
                    invocation.PackageId,
                    invocation.Channel,
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateExpectedResumeRelease(
            expectedResumeRequest,
            previousRelease);

        var state =
            await RequireInstalledStateAsync(
                    invocation.PackageId,
                    cancellationToken)
                .ConfigureAwait(false);

        var resumeRequest =
            expectedResumeRequest
            ?? CreateResumeRequest(
                invocation,
                previousRelease);

        await ExecuteResumableAsync(
                resumeRequest,
                () =>
                    _workflowService.RollbackAsync(
                        currentRelease,
                        previousRelease,
                        state,
                        stagingRoot,
                        progress,
                        cancellationToken,
                        resumeEntry),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PackageRelease> ResolveReleaseAsync(
        string manifestSource,
        PackageId packageId,
        ReleaseChannel channel,
        CancellationToken cancellationToken)
    {
        var provider =
            _providerFactory.Create(
                manifestSource);

        return await provider
            .GetLatestReleaseAsync(
                packageId,
                channel,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                ReleaseNotFoundMessage);
    }

    private async Task ExecuteResumableAsync(
        InstallerResumeRequest resumeRequest,
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        await _resumeRepository
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

    private async Task<InstalledPackageState> RequireInstalledStateAsync(
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

    private static void ValidateResumeCheckpoint(
        PackageId packageId,
        InstallerResumeRequest resumeRequest,
        InstallerOperationJournalEntry journal)
    {
        if (
            resumeRequest.PackageId != packageId ||
            journal.PackageId != packageId ||
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
                "Persisted CLI reboot resume request does not match operation journal.");
        }
    }

    private static void ValidateExpectedResumeRelease(
        InstallerResumeRequest? expectedResumeRequest,
        PackageRelease release)
    {
        if (expectedResumeRequest is null)
        {
            return;
        }

        if (
            !release.Version.Equals(
                expectedResumeRequest.ExpectedVersion) ||
            !string.Equals(
                release.Artifact.Digest.Sha256,
                expectedResumeRequest.ExpectedArtifactDigest.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Signed manifest release changed while CLI operation was waiting for reboot.");
        }
    }

    private static InstallerResumeRequest CreateResumeRequest(
        CliInvocation invocation,
        PackageRelease release)
    {
        return new InstallerResumeRequest(
            release.PackageId,
            invocation.Operation
                ?? throw new InvalidOperationException(
                    "CLI operation is required for resume request."),
            release.Version,
            release.Artifact.Digest,
            invocation.Channel,
            invocation.ManifestSource
                ?? throw new InvalidOperationException(
                    "Manifest source is required for resume request."),
            invocation.RollbackManifestSource,
            DateTimeOffset.UtcNow);
    }

    private static string ResolveInstallTarget(
        string? requestedTarget,
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
                "Install target is unavailable.");
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

        Directory.CreateDirectory(path);
        return path;
    }

    private async Task DeleteResumeRequestBestEffortAsync(
        PackageId packageId)
    {
        try
        {
            await _resumeRepository
                .DeleteAsync(
                    packageId,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
        }
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
