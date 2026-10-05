// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/viewmodels/MainWindowViewModel.cs
// 📌 Amac: TurkuazInstaller ana penceresinin bind edilebilir UI state modelini tasir
// 📌 Modul - ViewModel CSharp
// Version: 0.7.1
// Aciklama: Form alanlari, language label, progress, error ve recovery state'ini WinUI framework bagimsiz INotifyPropertyChanged modeliyle yonetir
//
// Bagimli Oldugu Katman: View | Language

using System.ComponentModel;
using System.Runtime.CompilerServices;
using TurkuazInstaller.Presentation.Language;

namespace TurkuazInstaller.Presentation.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<string> ChannelItems =
        Array.AsReadOnly(
            new[]
            {
                InstallerUiLabels.Stable,
                InstallerUiLabels.Beta
            });

    private string _packageIdText = string.Empty;
    private string _manifestSource = string.Empty;
    private string _rollbackManifestSource = string.Empty;
    private string _targetPath = string.Empty;
    private int _selectedChannelIndex;
    private int _progressValue;
    private string _statusMessage = InstallerUiLabels.Ready;
    private string _errorMessage = string.Empty;
    private bool _hasError;
    private bool _isBusy;
    private bool _canRetry;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WindowTitle => InstallerUiLabels.WindowTitle;
    public string HeaderTitle => InstallerUiLabels.HeaderTitle;
    public string HeaderSubtitle => InstallerUiLabels.HeaderSubtitle;
    public string PackageIdLabel => InstallerUiLabels.PackageId;
    public string ChannelLabel => InstallerUiLabels.Channel;
    public string ManifestSourceLabel => InstallerUiLabels.ManifestSource;
    public string RollbackManifestSourceLabel => InstallerUiLabels.RollbackManifestSource;
    public string TargetPathLabel => InstallerUiLabels.TargetPath;
    public string InstallLabel => InstallerUiLabels.Install;
    public string UpdateLabel => InstallerUiLabels.Update;
    public string RepairLabel => InstallerUiLabels.Repair;
    public string RollbackLabel => InstallerUiLabels.Rollback;
    public string RetryLabel => InstallerUiLabels.Retry;
    public string CancelLabel => InstallerUiLabels.Cancel;
    public string StatusTitle => InstallerUiLabels.StatusTitle;
    public string SourceTitle => InstallerUiLabels.SourceTitle;
    public string OperationsTitle => InstallerUiLabels.OperationsTitle;
    public string RecoveryTitle => InstallerUiLabels.RecoveryTitle;
    public string ManifestPlaceholder => InstallerUiLabels.ManifestPlaceholder;
    public string RollbackManifestPlaceholder => InstallerUiLabels.RollbackManifestPlaceholder;
    public string FooterLabel => InstallerUiLabels.Footer;
    public IReadOnlyList<string> ChannelOptions => ChannelItems;

    public string PackageIdText
    {
        get => _packageIdText;
        set => SetProperty(ref _packageIdText, value);
    }

    public string ManifestSource
    {
        get => _manifestSource;
        set => SetProperty(ref _manifestSource, value);
    }

    public string RollbackManifestSource
    {
        get => _rollbackManifestSource;
        set => SetProperty(ref _rollbackManifestSource, value);
    }

    public string TargetPath
    {
        get => _targetPath;
        set => SetProperty(ref _targetPath, value);
    }

    public int SelectedChannelIndex
    {
        get => _selectedChannelIndex;
        set => SetProperty(ref _selectedChannelIndex, value);
    }

    public int ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanRun));
                OnPropertyChanged(nameof(CanCancel));
            }
        }
    }

    public bool CanRun => !IsBusy;

    public bool CanCancel => IsBusy;

    public bool CanRetry
    {
        get => _canRetry;
        set => SetProperty(ref _canRetry, value);
    }

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
