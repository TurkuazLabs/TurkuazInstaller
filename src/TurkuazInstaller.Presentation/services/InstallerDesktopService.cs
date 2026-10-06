// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/InstallerDesktopService.cs
// 📌 Amac: Ana pencere startup resume, request validation, progress, cancel, retry ve error recovery is kurallarini yonetir
// 📌 Modul - Service CSharp
// Version: 1.3.0
// Aciklama: Manual/resume operasyonlarina ek olarak committed install state katalog refresh ve ayri katalog hata state'ini koordine eder
//
// Bagimli Oldugu Katman: Service | View | Language

using TurkuazInstaller.Application.Operations;
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
            null)
    {
    }

    public InstallerDesktopService(
        MainWindowViewModel viewModel,
        IInstallerRuntimeService runtimeService,
        InstallerResumeLaunchParser resumeLaunchParser,
        InstalledAppCatalogService? catalogService)
    {
        _viewModel = viewModel;
        _runtimeService = runtimeService;
        _resumeLaunchParser = resumeLaunchParser;
        _catalogService = catalogService;
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

    public async Task RunAsync(
        InstallerOperationKind operation)
    {
        if (_viewModel.IsBusy)
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

    public async Task RefreshInstalledAppsAsync()
    {
        if (_viewModel.IsBusy)
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
    }

    private InstallerDesktopRequest CreateRequest(
        InstallerOperationKind operation)
    {
        if (
            string.IsNullOrWhiteSpace(
                _viewModel.PackageIdText))
        {
            throw new InvalidOperationException(
                InstallerUiLabels.PackageIdRequired);
        }

        if (
            operation != InstallerOperationKind.Uninstall &&
            string.IsNullOrWhiteSpace(
                _viewModel.ManifestSource))
        {
            throw new InvalidOperationException(
                InstallerUiLabels.ManifestRequired);
        }

        if (
            operation == InstallerOperationKind.Rollback &&
            string.IsNullOrWhiteSpace(
                _viewModel.RollbackManifestSource))
        {
            throw new InvalidOperationException(
                InstallerUiLabels.RollbackManifestRequired);
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

            await RefreshInstalledAppsCoreAsync(
                    CancellationToken.None)
                .ConfigureAwait(true);

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
                InstallerUiLabels.Cancelled;
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
                InstallerUiLabels.Cancelled;
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
                    InstallerUiLabels.CatalogLoadFailedPrefix,
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
            InstallerUiLabels.Completed;
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
            InstallerUiLabels.Preparing;
    }

    private void ShowRebootRequired()
    {
        _viewModel.HasError = false;
        _viewModel.ErrorMessage = string.Empty;
        _viewModel.StatusMessage =
            InstallerUiLabels.RebootRequired;
        _viewModel.CanRetry = false;
    }

    private void ShowFailure(
        Exception exception)
    {
        _viewModel.HasError = true;
        _viewModel.ErrorMessage =
            string.Concat(
                InstallerUiLabels.OperationFailedPrefix,
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
                    InstallerUiLabels.Downloading,
                InstallerProgressStage.Verifying =>
                    InstallerUiLabels.Verifying,
                InstallerProgressStage.Staging =>
                    InstallerUiLabels.Staging,
                InstallerProgressStage.Applying =>
                    InstallerUiLabels.Applying,
                InstallerProgressStage.Uninstalling =>
                    InstallerUiLabels.Uninstalling,
                InstallerProgressStage.SavingState =>
                    InstallerUiLabels.SavingState,
                InstallerProgressStage.RemovingState =>
                    InstallerUiLabels.RemovingState,
                InstallerProgressStage.Completed =>
                    InstallerUiLabels.Completed,
                _ =>
                    InstallerUiLabels.Preparing
            };
    }
}
