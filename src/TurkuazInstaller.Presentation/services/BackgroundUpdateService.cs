// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/BackgroundUpdateService.cs
// 📌 Amac: WinUI session icinde configured package'lar icin periyodik read-only signed update check cycle'ini koordine eder
// 📌 Modul - Service CSharp
// Version: 1.1.0
// Aciklama: Mutation/artifact download baslatmaz; entry hatalarini izole eder ve tum cycle metinlerini typed Language katalogundan cozer
//
// Bagimli Oldugu Katman: Service | View | Config

using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.ViewModels;

namespace TurkuazInstaller.Presentation.Services;

public sealed class BackgroundUpdateService
    : IDisposable
{
    private readonly IInstallerRuntimeService
        _runtimeService;

    private readonly MainWindowViewModel
        _viewModel;

    private readonly BackgroundUpdatePolicy
        _policy;

    private readonly InstallerUiCatalog
        _ui;

    private readonly CancellationTokenSource
        _lifetimeCancellation =
            new();

    public BackgroundUpdateService(
        IInstallerRuntimeService runtimeService,
        MainWindowViewModel viewModel,
        BackgroundUpdatePolicy policy,
        InstallerUiCatalog? uiCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(
            runtimeService);
        ArgumentNullException.ThrowIfNull(
            viewModel);
        ArgumentNullException.ThrowIfNull(
            policy);

        _runtimeService =
            runtimeService;

        _viewModel =
            viewModel;

        _policy =
            policy;

        _ui =
            uiCatalog
            ?? new InstallerUiCatalog(
                InstallerUiProfile.Empty);

        _viewModel.BackgroundUpdateStatus =
            IsEnabled
                ? _ui.Resolve(
                    InstallerUiLabelKey.BackgroundUpdatesWaiting)
                : _ui.Resolve(
                    InstallerUiLabelKey.BackgroundUpdatesDisabled);
    }

    public bool IsEnabled =>
        _policy.Enabled &&
        _policy.Entries.Count > 0;

    public TimeSpan Interval =>
        _policy.Interval;

    public async Task RunOnceAsync()
    {
        if (!IsEnabled)
        {
            _viewModel.BackgroundUpdateStatus =
                _ui.Resolve(
                    InstallerUiLabelKey.BackgroundUpdatesDisabled);

            return;
        }

        if (
            _viewModel.IsBusy ||
            _viewModel.IsCheckingUpdate ||
            _viewModel.IsBackgroundUpdateCheckRunning)
        {
            return;
        }

        var cancellationToken =
            _lifetimeCancellation.Token;

        _viewModel.IsBackgroundUpdateCheckRunning =
            true;

        _viewModel.BackgroundUpdateStatus =
            _ui.Resolve(
                InstallerUiLabelKey.BackgroundUpdatesChecking);

        var checkedCount = 0;
        var availableCount = 0;
        var failureCount = 0;

        try
        {
            foreach (var entry in
                     _policy.Entries)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                try
                {
                    var result =
                        await _runtimeService
                            .CheckUpdateAsync(
                                new InstallerUpdateCheckRequest(
                                    entry.PackageId.Value,
                                    entry.Channel,
                                    entry.ManifestSource),
                                cancellationToken)
                            .ConfigureAwait(true);

                    checkedCount++;

                    if (
                        result.Availability ==
                            UpdateAvailability.Available &&
                        result.InstalledState is not null)
                    {
                        availableCount++;
                    }
                }
                catch (OperationCanceledException)
                    when (_lifetimeCancellation
                        .IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    failureCount++;
                }
            }

            _viewModel.BackgroundUpdateStatus =
                string.Concat(
                    _ui.Resolve(
                        InstallerUiLabelKey.BackgroundUpdatesCompletedPrefix),
                    ": ",
                    checkedCount,
                    " ",
                    _ui.Resolve(
                        InstallerUiLabelKey.BackgroundUpdatesCheckedPrefix),
                    ", ",
                    availableCount,
                    " ",
                    _ui.Resolve(
                        InstallerUiLabelKey.BackgroundUpdatesAvailablePrefix),
                    ", ",
                    failureCount,
                    " ",
                    _ui.Resolve(
                        InstallerUiLabelKey.BackgroundUpdatesFailurePrefix));
        }
        finally
        {
            _viewModel.IsBackgroundUpdateCheckRunning =
                false;
        }
    }

    public void Dispose()
    {
        if (!_lifetimeCancellation.IsCancellationRequested)
        {
            _lifetimeCancellation.Cancel();
        }
    }
}
