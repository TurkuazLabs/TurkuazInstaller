// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/viewmodels/MainWindowViewModel.cs
// 📌 Amac: TurkuazInstaller ana penceresinin bind edilebilir UI state modelini tasir
// 📌 Modul - ViewModel CSharp
// Version: 1.4.0
// Aciklama: Runtime state'e ek olarak typed branding/localization UI profilini fallback-guvenli uygular
//
// Bagimli Oldugu Katman: View | Language

using System.ComponentModel;
using System.Runtime.CompilerServices;
using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Presentation.Language;

namespace TurkuazInstaller.Presentation.ViewModels;

public sealed class MainWindowViewModel
    : INotifyPropertyChanged
{
    private readonly InstallerUiCatalog _ui;

    private static readonly IReadOnlyList<string> ChannelItems =
        Array.AsReadOnly(
            new[]
            {
                InstallerUiLabels.Stable,
                InstallerUiLabels.Beta
            });

    private IReadOnlyList<InstalledAppListItemViewModel>
        _installedApps =
            Array.Empty<InstalledAppListItemViewModel>();

    private string _catalogErrorMessage =
        string.Empty;

    private bool _hasCatalogError;

    private string _installedVersionText =
        InstallerUiLabels.VersionUnavailable;

    private string _latestVersionText =
        InstallerUiLabels.VersionUnavailable;

    private string _updateDiscoveryStatus =
        InstallerUiLabels.UpdateNotChecked;

    private string _updateDiscoveryErrorMessage =
        string.Empty;

    private bool _hasUpdateDiscoveryError;
    private bool _hasUpdateAvailable;
    private bool _isCheckingUpdate;

    private string _backgroundUpdateStatus =
        InstallerUiLabels.BackgroundUpdatesDisabled;

    private bool _isBackgroundUpdateCheckRunning;

    private string _packageIdText = string.Empty;
    private string _manifestSource = string.Empty;
    private string _rollbackManifestSource = string.Empty;
    private string _targetPath = string.Empty;
    private int _selectedChannelIndex;
    private int _progressValue;
    private string _statusMessage =
        InstallerUiLabels.Ready;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _isBusy;
    private bool _canRetry;

    public MainWindowViewModel()
        : this(
            InstallerUiProfile.Empty)
    {
    }

    public MainWindowViewModel(
        InstallerUiProfile uiProfile)
    {
        _ui =
            new InstallerUiCatalog(
                uiProfile);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WindowTitle =>
        _ui.WindowTitle;

    public string HeaderTitle =>
        _ui.HeaderTitle;

    public string HeaderSubtitle =>
        _ui.HeaderSubtitle;

    public string InstalledAppsTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.InstalledAppsTitle,
            InstallerUiLabels.InstalledAppsTitle);

    public string RefreshInstalledAppsLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.RefreshInstalledApps,
            InstallerUiLabels.RefreshInstalledApps);

    public string InstalledAppsCountText =>
        string.Concat(
            InstallerUiLabels.InstalledAppsCountPrefix,
            " ",
            InstalledApps.Count);

    public string UpdateDiscoveryTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.UpdateDiscoveryTitle,
            InstallerUiLabels.UpdateDiscoveryTitle);

    public string CheckUpdatesLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.CheckUpdates,
            InstallerUiLabels.CheckUpdates);

    public string InstalledVersionLabel =>
        InstallerUiLabels.InstalledVersion;

    public string LatestVersionLabel =>
        InstallerUiLabels.LatestVersion;

    public string BackgroundUpdatesTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.BackgroundUpdatesTitle,
            InstallerUiLabels.BackgroundUpdatesTitle);

    public string PackageIdLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.PackageId,
            InstallerUiLabels.PackageId);

    public string ChannelLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Channel,
            InstallerUiLabels.Channel);

    public string ManifestSourceLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.ManifestSource,
            InstallerUiLabels.ManifestSource);

    public string RollbackManifestSourceLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.RollbackManifestSource,
            InstallerUiLabels.RollbackManifestSource);

    public string TargetPathLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.TargetPath,
            InstallerUiLabels.TargetPath);

    public string InstallLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Install,
            InstallerUiLabels.Install);

    public string UpdateLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Update,
            InstallerUiLabels.Update);

    public string RepairLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Repair,
            InstallerUiLabels.Repair);

    public string RollbackLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Rollback,
            InstallerUiLabels.Rollback);

    public string UninstallLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Uninstall,
            InstallerUiLabels.Uninstall);

    public string RetryLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Retry,
            InstallerUiLabels.Retry);

    public string CancelLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Cancel,
            InstallerUiLabels.Cancel);

    public string StatusTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.StatusTitle,
            InstallerUiLabels.StatusTitle);

    public string SourceTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.SourceTitle,
            InstallerUiLabels.SourceTitle);

    public string OperationsTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.OperationsTitle,
            InstallerUiLabels.OperationsTitle);

    public string RecoveryTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.RecoveryTitle,
            InstallerUiLabels.RecoveryTitle);

    public string ManifestPlaceholder =>
        InstallerUiLabels.ManifestPlaceholder;

    public string RollbackManifestPlaceholder =>
        InstallerUiLabels.RollbackManifestPlaceholder;

    public string TargetPathPlaceholder =>
        InstallerUiLabels.TargetPathPlaceholder;

    public string FooterLabel =>
        _ui.Footer;

    public IReadOnlyList<string> ChannelOptions =>
        ChannelItems;

    public IReadOnlyList<InstalledAppListItemViewModel>
        InstalledApps
    {
        get => _installedApps;
        private set
        {
            if (
                SetProperty(
                    ref _installedApps,
                    value))
            {
                OnPropertyChanged(
                    nameof(InstalledAppsCountText));
            }
        }
    }

    public string CatalogErrorMessage
    {
        get => _catalogErrorMessage;
        set => SetProperty(
            ref _catalogErrorMessage,
            value);
    }

    public bool HasCatalogError
    {
        get => _hasCatalogError;
        set => SetProperty(
            ref _hasCatalogError,
            value);
    }

    public string PackageIdText
    {
        get => _packageIdText;
        set
        {
            if (
                SetProperty(
                    ref _packageIdText,
                    value))
            {
                ResetUpdateDiscovery();
            }
        }
    }

    public string ManifestSource
    {
        get => _manifestSource;
        set
        {
            if (
                SetProperty(
                    ref _manifestSource,
                    value))
            {
                ResetUpdateDiscovery();
            }
        }
    }

    public string RollbackManifestSource
    {
        get => _rollbackManifestSource;
        set => SetProperty(
            ref _rollbackManifestSource,
            value);
    }

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(
            ref _targetPath,
            value);
    }

    public int SelectedChannelIndex
    {
        get => _selectedChannelIndex;
        set
        {
            if (
                SetProperty(
                    ref _selectedChannelIndex,
                    value))
            {
                ResetUpdateDiscovery();
            }
        }
    }

    public int ProgressValue
    {
        get => _progressValue;
        set => SetProperty(
            ref _progressValue,
            value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(
            ref _statusMessage,
            value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(
            ref _errorMessage,
            value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(
            ref _hasError,
            value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (
                SetProperty(
                    ref _isBusy,
                    value))
            {
                OnPropertyChanged(
                    nameof(CanRun));
                OnPropertyChanged(
                    nameof(CanCancel));
                OnPropertyChanged(
                    nameof(CanCheckUpdate));
            }
        }
    }

    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        set
        {
            if (
                SetProperty(
                    ref _isCheckingUpdate,
                    value))
            {
                OnPropertyChanged(
                    nameof(CanRun));
                OnPropertyChanged(
                    nameof(CanCheckUpdate));
            }
        }
    }

    public bool IsBackgroundUpdateCheckRunning
    {
        get => _isBackgroundUpdateCheckRunning;
        set
        {
            if (
                SetProperty(
                    ref _isBackgroundUpdateCheckRunning,
                    value))
            {
                OnPropertyChanged(
                    nameof(CanRun));
                OnPropertyChanged(
                    nameof(CanCheckUpdate));
            }
        }
    }

    public string BackgroundUpdateStatus
    {
        get => _backgroundUpdateStatus;
        set => SetProperty(
            ref _backgroundUpdateStatus,
            value);
    }

    public bool CanRun =>
        !IsBusy &&
        !IsCheckingUpdate &&
        !IsBackgroundUpdateCheckRunning;

    public bool CanCancel => IsBusy;

    public bool CanCheckUpdate =>
        !IsBusy &&
        !IsCheckingUpdate &&
        !IsBackgroundUpdateCheckRunning;

    public string InstalledVersionText
    {
        get => _installedVersionText;
        set => SetProperty(
            ref _installedVersionText,
            value);
    }

    public string LatestVersionText
    {
        get => _latestVersionText;
        set => SetProperty(
            ref _latestVersionText,
            value);
    }

    public string UpdateDiscoveryStatus
    {
        get => _updateDiscoveryStatus;
        set => SetProperty(
            ref _updateDiscoveryStatus,
            value);
    }

    public string UpdateDiscoveryErrorMessage
    {
        get => _updateDiscoveryErrorMessage;
        set => SetProperty(
            ref _updateDiscoveryErrorMessage,
            value);
    }

    public bool HasUpdateDiscoveryError
    {
        get => _hasUpdateDiscoveryError;
        set => SetProperty(
            ref _hasUpdateDiscoveryError,
            value);
    }

    public bool HasUpdateAvailable
    {
        get => _hasUpdateAvailable;
        set => SetProperty(
            ref _hasUpdateAvailable,
            value);
    }

    public void SetInstalledApps(
        IReadOnlyList<InstalledAppListItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(
            items);

        InstalledApps =
            Array.AsReadOnly(
                items.ToArray());
    }

    public bool CanRetry
    {
        get => _canRetry;
        set => SetProperty(
            ref _canRetry,
            value);
    }

    private void ResetUpdateDiscovery()
    {
        InstalledVersionText =
            InstallerUiLabels.VersionUnavailable;

        LatestVersionText =
            InstallerUiLabels.VersionUnavailable;

        UpdateDiscoveryStatus =
            InstallerUiLabels.UpdateNotChecked;

        UpdateDiscoveryErrorMessage =
            string.Empty;

        HasUpdateDiscoveryError = false;
        HasUpdateAvailable = false;
    }

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName]
        string? propertyName = null)
    {
        if (
            EqualityComparer<T>.Default.Equals(
                storage,
                value))
        {
            return false;
        }

        storage = value;

        OnPropertyChanged(
            propertyName);

        return true;
    }

    private void OnPropertyChanged(
        [CallerMemberName]
        string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}
