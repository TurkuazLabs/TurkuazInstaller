// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/viewmodels/MainWindowViewModel.cs
// 📌 Amac: TurkuazInstaller ana penceresinin bind edilebilir UI state modelini tasir
// 📌 Modul - ViewModel CSharp
// Version: 1.5.0
// Aciklama: Branding, channel, placeholder ve runtime state metinlerini tek typed localization katalogundan fallback-guvenli cozer
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

    private readonly IReadOnlyList<string>
        _channelItems;

    private IReadOnlyList<InstalledAppListItemViewModel>
        _installedApps =
            Array.Empty<InstalledAppListItemViewModel>();

    private string _catalogErrorMessage =
        string.Empty;

    private bool _hasCatalogError;

    private string _installedVersionText;

    private string _latestVersionText;

    private string _updateDiscoveryStatus;

    private string _updateDiscoveryErrorMessage =
        string.Empty;

    private bool _hasUpdateDiscoveryError;
    private bool _hasUpdateAvailable;
    private bool _isCheckingUpdate;

    private string _backgroundUpdateStatus;

    private bool _isBackgroundUpdateCheckRunning;

    private string _packageIdText = string.Empty;
    private string _manifestSource = string.Empty;
    private string _rollbackManifestSource = string.Empty;
    private string _targetPath = string.Empty;
    private int _selectedChannelIndex;
    private int _progressValue;
    private string _statusMessage;
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
        : this(
            new InstallerUiCatalog(
                uiProfile))
    {
    }

    public MainWindowViewModel(
        InstallerUiCatalog uiCatalog)
    {
        ArgumentNullException.ThrowIfNull(
            uiCatalog);

        _ui =
            uiCatalog;

        _channelItems =
            Array.AsReadOnly(
                new[]
                {
                    _ui.Resolve(
                        InstallerUiLabelKey.Stable),
                    _ui.Resolve(
                        InstallerUiLabelKey.Beta)
                });

        _installedVersionText =
            _ui.Resolve(
                InstallerUiLabelKey.VersionUnavailable);

        _latestVersionText =
            _ui.Resolve(
                InstallerUiLabelKey.VersionUnavailable);

        _updateDiscoveryStatus =
            _ui.Resolve(
                InstallerUiLabelKey.UpdateNotChecked);

        _backgroundUpdateStatus =
            _ui.Resolve(
                InstallerUiLabelKey.BackgroundUpdatesDisabled);

        _statusMessage =
            _ui.Resolve(
                InstallerUiLabelKey.Ready);
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
            InstallerUiLabelKey.InstalledAppsTitle);

    public string RefreshInstalledAppsLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.RefreshInstalledApps);

    public string InstalledAppsCountText =>
        string.Concat(
            _ui.Resolve(
                InstallerUiLabelKey.InstalledAppsCountPrefix),
            " ",
            InstalledApps.Count);

    public string UpdateDiscoveryTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.UpdateDiscoveryTitle);

    public string CheckUpdatesLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.CheckUpdates);

    public string InstalledVersionLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.InstalledVersion);

    public string LatestVersionLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.LatestVersion);

    public string BackgroundUpdatesTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.BackgroundUpdatesTitle);

    public string PackageIdLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.PackageId);

    public string ChannelLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Channel);

    public string ManifestSourceLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.ManifestSource);

    public string RollbackManifestSourceLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.RollbackManifestSource);

    public string TargetPathLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.TargetPath);

    public string InstallLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Install);

    public string UpdateLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Update);

    public string RepairLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Repair);

    public string RollbackLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Rollback);

    public string UninstallLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Uninstall);

    public string RetryLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Retry);

    public string CancelLabel =>
        _ui.Resolve(
            InstallerUiLabelKey.Cancel);

    public string StatusTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.StatusTitle);

    public string SourceTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.SourceTitle);

    public string OperationsTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.OperationsTitle);

    public string RecoveryTitle =>
        _ui.Resolve(
            InstallerUiLabelKey.RecoveryTitle);

    public string ManifestPlaceholder =>
        _ui.Resolve(
            InstallerUiLabelKey.ManifestPlaceholder);

    public string RollbackManifestPlaceholder =>
        _ui.Resolve(
            InstallerUiLabelKey.RollbackManifestPlaceholder);

    public string TargetPathPlaceholder =>
        _ui.Resolve(
            InstallerUiLabelKey.TargetPathPlaceholder);

    public string FooterLabel =>
        _ui.Footer;

    public IReadOnlyList<string> ChannelOptions =>
        _channelItems;

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
            _ui.Resolve(
                InstallerUiLabelKey.VersionUnavailable);

        LatestVersionText =
            _ui.Resolve(
                InstallerUiLabelKey.VersionUnavailable);

        UpdateDiscoveryStatus =
            _ui.Resolve(
                InstallerUiLabelKey.UpdateNotChecked);

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
