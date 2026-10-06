// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/InstallerDesktopService.cs
// 📌 Amac: Ana pencere startup resume, request validation, progress, cancel, retry ve error recovery is kurallarini yonetir
// 📌 Modul - Service CSharp
// Version: 1.7.0
// Aciklama: Manual/resume, catalog, background update ve discovery/mutation UX akislarinda tum kullanici metinlerini typed Language katalogundan cozer
//
// Bagimli Oldugu Katman: Service | View | Language

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.ViewModels;

namespace TurkuazInstaller.Presentation.Services;

public sealed class InstallerDesktopService
    : IDisposable
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IInstallerRuntimeService _runtimeService;
    private readonly InstallerResumeLaunchParser _resumeLaunchParser;
    private readonly InstalledAppCatalogService? _catalogService;
    private readonly BackgroundUpdateService? _backgroundUpdateService;
    private readonly InstallerUiCatalog _ui;
    private CancellationTokenSource? _operationCancellation;
    private InstallerDesktopRequest? _lastRequest;

    public InstallerDesktopService(
        MainWindowViewModel viewModel,
        IInstallerRuntimeService runtimeService,
        InstallerResumeLaunchParser resumeLaunchParser)
        : this(
            viewModel,
            runtimeService,
            resumeLaunchParser,
            null,
            null)
    {
    }

    public InstallerDesktopService(
        MainWindowViewModel viewModel,
        IInstallerRuntimeService runtimeService,
        InstallerResumeLaunchParser resumeLaunchParser,
        InstalledAppCatalogService? catalogService)
        : this(
            viewModel,
            runtimeService,
            resumeLaunchParser,
            catalogService,
            null)
    {
    }

    public InstallerDesktopService(
        MainWindowViewModel viewModel,
        IInstallerRuntimeService runtimeService,
        InstallerResumeLaunchParser resumeLaunchParser,
        InstalledAppCatalogService? catalogService,
        BackgroundUpdateService? backgroundUpdateService,
        InstallerUiCatalog? uiCatalog = null)
    {
        _viewModel = viewModel;
        _runtimeService = runtimeService;
        _resumeLaunchParser = resumeLaunchParser;
        _catalogService = catalogService;
        _backgroundUpdateService = backgroundUpdateService;

        _ui =
            uiCatalog
            ?? new InstallerUiCatalog(
                InstallerUiProfile.Empty);
    }

    public async Task StartAsync(
        IReadOnlyList<string> launchArguments)
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        PackageId? resumePackageId;

        try
        {
            resumePackageId =
                _resumeLaunchParser.Parse(
                    launchArguments);
        }
        catch (Exception exception)
        {
            ShowFailure(
                exception);
            return;
        }

        if (resumePackageId is null)
        {
            ClearUpdateDiscoveryResult();

            await RefreshInstalledAppsCoreAsync(
                    CancellationToken.None)
                .ConfigureAwait(true);

            return;
        }

        _lastRequest = null;

        await ExecuteResumeAsync(
                resumePackageId)
            .ConfigureAwait(true);
    }

    public TimeSpan? BackgroundUpdateInterval =>
        _backgroundUpdateService?.IsEnabled == true
            ? _backgroundUpdateService.Interval
            : null;

    public Task RunBackgroundUpdateCheckAsync()
    {
        return _backgroundUpdateService
            ?.RunOnceAsync()
            ?? Task.CompletedTask;
    }

    public async Task RunAsync(
        InstallerOperationKind operation)
    {
        if (
            _viewModel.IsBusy ||
            _viewModel.IsCheckingUpdate ||
            _viewModel.IsBackgroundUpdateCheckRunning)
        {
            return;
        }

        InstallerDesktopRequest request;

        try
        {
            request =
                CreateRequest(
                    operation);
        }
        catch (Exception exception)
        {
            ShowFailure(
                exception);
            return;
        }

        _lastRequest = request;

        await ExecuteRequestAsync(
                request)
            .ConfigureAwait(true);
    }

    public async Task CheckForUpdatesAsync()
    {
        if (
            _viewModel.IsBusy ||
            _viewModel.IsCheckingUpdate ||
            _viewModel.IsBackgroundUpdateCheckRunning)
        {
            return;
        }

        _viewModel.IsCheckingUpdate = true;
        _viewModel.HasUpdateDiscoveryError = false;
        _viewModel.UpdateDiscoveryErrorMessage =
            string.Empty;
        _viewModel.UpdateDiscoveryStatus =
            _ui.Resolve(InstallerUiLabelKey.CheckingForUpdates);

        try
        {
            var request =
                CreateUpdateCheckRequest();

            var result =
                await _runtimeService
                    .CheckUpdateAsync(
                        request,
                        CancellationToken.None)
                    .ConfigureAwait(true);

            ApplyUpdateCheckResult(
                result);
        }
        catch (Exception exception)
        {
            _viewModel.HasUpdateDiscoveryError = true;
            _viewModel.UpdateDiscoveryErrorMessage =
                string.Concat(
                    _ui.Resolve(InstallerUiLabelKey.UpdateCheckFailedPrefix),
                    " ",
                    exception.Message);

            _viewModel.UpdateDiscoveryStatus =
                _ui.Resolve(InstallerUiLabelKey.UpdateNotChecked);

            _viewModel.HasUpdateAvailable = false;
        }
        finally
        {
            _viewModel.IsCheckingUpdate = false;
        }
    }

    public async Task RefreshInstalledAppsAsync()
    {
        if (
            _viewModel.IsBusy ||
            _viewModel.IsCheckingUpdate ||
            _viewModel.IsBackgroundUpdateCheckRunning)
        {
            return;
        }

        await RefreshInstalledAppsCoreAsync(
                CancellationToken.None)
            .ConfigureAwait(true);
    }

    public async Task RetryAsync()
    {
        if (
            _viewModel.IsBusy ||
            _viewModel.IsCheckingUpdate ||
            _viewModel.IsBackgroundUpdateCheckRunning ||
            _lastRequest is null)
        {
            return;
        }

        await ExecuteRequestAsync(
                _lastRequest)
            .ConfigureAwait(true);
    }

    public void Cancel()
    {
        _operationCancellation?.Cancel();
    }

    public void Dispose()
    {
        _operationCancellation?.Dispose();
        _backgroundUpdateService?.Dispose();
    }

    private InstallerUpdateCheckRequest CreateUpdateCheckRequest()
    {
        if (
            string.IsNullOrWhiteSpace(
                _viewModel.PackageIdText))
        {
            throw new InvalidOperationException(
                _ui.Resolve(InstallerUiLabelKey.PackageIdRequired));
        }

        if (
            string.IsNullOrWhiteSpace(
                _viewModel.ManifestSource))
        {
            throw new InvalidOperationException(
                _ui.Resolve(InstallerUiLabelKey.ManifestRequired));
        }

        var channel =
            _viewModel.SelectedChannelIndex == 1
                ? ReleaseChannel.Beta
                : ReleaseChannel.Stable;

        return new InstallerUpdateCheckRequest(
            _viewModel.PackageIdText.Trim(),
            channel,
            _viewModel.ManifestSource.Trim());
    }

    private InstallerDesktopRequest CreateRequest(
        InstallerOperationKind operation)
    {
        if (
            string.IsNullOrWhiteSpace(
                _viewModel.PackageIdText))
        {
            throw new InvalidOperationException(
                _ui.Resolve(InstallerUiLabelKey.PackageIdRequired));
        }

        if (
            operation != InstallerOperationKind.Uninstall &&
            string.IsNullOrWhiteSpace(
                _viewModel.ManifestSource))
        {
            throw new InvalidOperationException(
                _ui.Resolve(InstallerUiLabelKey.ManifestRequired));
        }

        if (
            operation == InstallerOperationKind.Rollback &&
            string.IsNullOrWhiteSpace(
                _viewModel.RollbackManifestSource))
        {
            throw new InvalidOperationException(
                _ui.Resolve(InstallerUiLabelKey.RollbackManifestRequired));
        }

        var channel =
            _viewModel.SelectedChannelIndex == 1
                ? ReleaseChannel.Beta
                : ReleaseChannel.Stable;

        return new InstallerDesktopRequest(
            operation,
            _viewModel.PackageIdText.Trim(),
            channel,
            _viewModel.ManifestSource.Trim(),
            string.IsNullOrWhiteSpace(
                _viewModel.RollbackManifestSource)
                ? null
                : _viewModel.RollbackManifestSource.Trim(),
            _viewModel.TargetPath.Trim());
    }

    private async Task ExecuteRequestAsync(
        InstallerDesktopRequest request)
    {
        ResetForOperation();

        _operationCancellation =
            new CancellationTokenSource();

        var progress =
            new OrderedProgress<InstallerOperationProgress>(
                ReportProgress);

        try
        {
            await _runtimeService
                .ExecuteAsync(
                    request,
                    progress,
                    _operationCancellation.Token)
                .ConfigureAwait(true);

            await CompleteUiAsync(
                    progress)
                .ConfigureAwait(true);

            ClearUpdateDiscoveryResult();

            await RefreshInstalledAppsCoreAsync(
                    CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (InstallerRebootRequiredException)
        {
            await progress
                .DrainAsync()
                .ConfigureAwait(true);

            ShowRebootRequired();
        }
        catch (OperationCanceledException)
        {
            await progress
                .DrainAsync()
                .ConfigureAwait(true);

            _viewModel.StatusMessage =
                _ui.Resolve(InstallerUiLabelKey.Cancelled);
            _viewModel.CanRetry = true;
        }
        catch (Exception exception)
        {
            await progress
                .DrainAsync()
                .ConfigureAwait(true);

            ShowFailure(
                exception);
        }
        finally
        {
            FinishOperation();
        }
    }

    private async Task ExecuteResumeAsync(
        PackageId packageId)
    {
        ResetForOperation();

        _operationCancellation =
            new CancellationTokenSource();

        var progress =
            new OrderedProgress<InstallerOperationProgress>(
                ReportProgress);

        try
        {
            await _runtimeService
                .ResumeAsync(
                    packageId,
                    progress,
                    _operationCancellation.Token)
                .ConfigureAwait(true);

            await CompleteUiAsync(
                    progress)
                .ConfigureAwait(true);

            ClearUpdateDiscoveryResult();

            await RefreshInstalledAppsCoreAsync(
                    CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (InstallerRebootRequiredException)
        {
            await progress
                .DrainAsync()
                .ConfigureAwait(true);

            ShowRebootRequired();
        }
        catch (OperationCanceledException)
        {
            await progress
                .DrainAsync()
                .ConfigureAwait(true);

            _viewModel.StatusMessage =
                _ui.Resolve(InstallerUiLabelKey.Cancelled);
            _viewModel.CanRetry = false;
        }
        catch (Exception exception)
        {
            await progress
                .DrainAsync()
                .ConfigureAwait(true);

            ShowFailure(
                exception);
        }
        finally
        {
            FinishOperation();
        }
    }

    private void ApplyUpdateCheckResult(
        UpdateCheckResult result)
    {
        _viewModel.InstalledVersionText =
            result.InstalledState?.Version.ToString()
            ?? _ui.Resolve(InstallerUiLabelKey.VersionUnavailable);

        _viewModel.LatestVersionText =
            result.LatestRelease?.Version.ToString()
            ?? _ui.Resolve(InstallerUiLabelKey.VersionUnavailable);

        _viewModel.HasUpdateAvailable =
            result.Availability ==
                UpdateAvailability.Available &&
            result.InstalledState is not null;

        _viewModel.UpdateDiscoveryStatus =
            result.Availability switch
            {
                UpdateAvailability.ReleaseNotFound =>
                    _ui.Resolve(InstallerUiLabelKey.UpdateReleaseNotFound),
                UpdateAvailability.Available
                    when result.InstalledState is null =>
                    _ui.Resolve(InstallerUiLabelKey.UpdateNotInstalled),
                UpdateAvailability.Available =>
                    _ui.Resolve(InstallerUiLabelKey.UpdateAvailable),
                UpdateAvailability.Current =>
                    _ui.Resolve(InstallerUiLabelKey.UpdateCurrent),
                UpdateAvailability.Skipped =>
                    _ui.Resolve(InstallerUiLabelKey.UpdateSkipped),
                UpdateAvailability.Pinned =>
                    _ui.Resolve(InstallerUiLabelKey.UpdatePinned),
                _ =>
                    _ui.Resolve(InstallerUiLabelKey.UpdateNotChecked)
            };

        _viewModel.HasUpdateDiscoveryError = false;
        _viewModel.UpdateDiscoveryErrorMessage =
            string.Empty;
    }

    private void ClearUpdateDiscoveryResult()
    {
        _viewModel.InstalledVersionText =
            _ui.Resolve(InstallerUiLabelKey.VersionUnavailable);
        _viewModel.LatestVersionText =
            _ui.Resolve(InstallerUiLabelKey.VersionUnavailable);
        _viewModel.UpdateDiscoveryStatus =
            _ui.Resolve(InstallerUiLabelKey.UpdateNotChecked);
        _viewModel.HasUpdateAvailable = false;
        _viewModel.HasUpdateDiscoveryError = false;
        _viewModel.UpdateDiscoveryErrorMessage =
            string.Empty;
    }

    private async Task RefreshInstalledAppsCoreAsync(
        CancellationToken cancellationToken)
    {
        if (_catalogService is null)
        {
            return;
        }

        try
        {
            var items =
                await _catalogService
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(true);

            _viewModel.SetInstalledApps(
                items);

            _viewModel.HasCatalogError = false;
            _viewModel.CatalogErrorMessage =
                string.Empty;
        }
        catch (Exception exception)
        {
            _viewModel.HasCatalogError = true;
            _viewModel.CatalogErrorMessage =
                string.Concat(
                    _ui.Resolve(InstallerUiLabelKey.CatalogLoadFailedPrefix),
                    " ",
                    exception.Message);
        }
    }

    private async Task CompleteUiAsync(
        OrderedProgress<InstallerOperationProgress> progress)
    {
        await progress
            .DrainAsync()
            .ConfigureAwait(true);

        _viewModel.ProgressValue = 100;
        _viewModel.StatusMessage =
            _ui.Resolve(InstallerUiLabelKey.Completed);
        _viewModel.CanRetry = false;
    }

    private void FinishOperation()
    {
        _viewModel.IsBusy = false;
        _operationCancellation?.Dispose();
        _operationCancellation = null;
    }

    private void ResetForOperation()
    {
        _viewModel.IsBusy = true;
        _viewModel.CanRetry = false;
        _viewModel.HasError = false;
        _viewModel.ErrorMessage = string.Empty;
        _viewModel.ProgressValue = 0;
        _viewModel.StatusMessage =
            _ui.Resolve(InstallerUiLabelKey.Preparing);
    }

    private void ShowRebootRequired()
    {
        _viewModel.HasError = false;
        _viewModel.ErrorMessage = string.Empty;
        _viewModel.StatusMessage =
            _ui.Resolve(InstallerUiLabelKey.RebootRequired);
        _viewModel.CanRetry = false;
    }

    private void ShowFailure(
        Exception exception)
    {
        _viewModel.HasError = true;
        _viewModel.ErrorMessage =
            string.Concat(
                _ui.Resolve(InstallerUiLabelKey.OperationFailedPrefix),
                " ",
                exception.Message);
        _viewModel.CanRetry =
            _lastRequest is not null;
    }

    private void ReportProgress(
        InstallerOperationProgress progress)
    {
        _viewModel.ProgressValue =
            progress.Percent;

        _viewModel.StatusMessage =
            progress.Stage switch
            {
                InstallerProgressStage.Downloading =>
                    _ui.Resolve(InstallerUiLabelKey.Downloading),
                InstallerProgressStage.Verifying =>
                    _ui.Resolve(InstallerUiLabelKey.Verifying),
                InstallerProgressStage.Staging =>
                    _ui.Resolve(InstallerUiLabelKey.Staging),
                InstallerProgressStage.Applying =>
                    _ui.Resolve(InstallerUiLabelKey.Applying),
                InstallerProgressStage.Uninstalling =>
                    _ui.Resolve(InstallerUiLabelKey.Uninstalling),
                InstallerProgressStage.SavingState =>
                    _ui.Resolve(InstallerUiLabelKey.SavingState),
                InstallerProgressStage.RemovingState =>
                    _ui.Resolve(InstallerUiLabelKey.RemovingState),
                InstallerProgressStage.Completed =>
                    _ui.Resolve(InstallerUiLabelKey.Completed),
                _ =>
                    _ui.Resolve(InstallerUiLabelKey.Preparing)
            };
    }
}
