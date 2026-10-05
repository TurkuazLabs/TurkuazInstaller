// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/services/WinUiInstallerRuntimeService.cs
// 📌 Amac: Presentation desktop requestlerini gercek provider, workflow, repository ve Velopack runtime operasyonlarina baglar
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Install target fallback, install/update/repair/rollback ve manifestsiz uninstall requestlerini Application workflow uzerinden calistirir
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.State;
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

    private readonly ManifestReleaseProviderFactory _providerFactory;
    private readonly InstallerWorkflowService _workflowService;
    private readonly IInstallStateRepository _stateRepository;
    private readonly DesktopRuntimeOptions _options;

    public WinUiInstallerRuntimeService(
        ManifestReleaseProviderFactory providerFactory,
        InstallerWorkflowService workflowService,
        IInstallStateRepository stateRepository,
        DesktopRuntimeOptions options)
    {
        _providerFactory = providerFactory;
        _workflowService = workflowService;
        _stateRepository = stateRepository;
        _options = options;
    }

    public async Task ExecuteAsync(
        InstallerDesktopRequest request,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);

        var packageId =
            PackageId.Parse(
                request.PackageId);

        if (
            request.Operation ==
            InstallerOperationKind.Uninstall)
        {
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
                    await _workflowService
                        .InstallAsync(
                            release,
                            ResolveInstallTarget(
                                request.TargetPath,
                                release),
                            operationStagingRoot,
                            progress,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case InstallerOperationKind.Update:
                    await _workflowService
                        .UpdateAsync(
                            release,
                            await RequireInstalledStateAsync(
                                    packageId,
                                    cancellationToken)
                                .ConfigureAwait(false),
                            operationStagingRoot,
                            progress,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case InstallerOperationKind.Repair:
                    await _workflowService
                        .RepairAsync(
                            release,
                            await RequireInstalledStateAsync(
                                    packageId,
                                    cancellationToken)
                                .ConfigureAwait(false),
                            operationStagingRoot,
                            progress,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case InstallerOperationKind.Rollback:
                    await ExecuteRollbackAsync(
                            request,
                            packageId,
                            release,
                            operationStagingRoot,
                            progress,
                            cancellationToken)
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
        CancellationToken cancellationToken)
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

        var state =
            await RequireInstalledStateAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        await _workflowService
            .RollbackAsync(
                currentRelease,
                previousRelease,
                state,
                operationStagingRoot,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
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

        Directory.CreateDirectory(path);
        return path;
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
