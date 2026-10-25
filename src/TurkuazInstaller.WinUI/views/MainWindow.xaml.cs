// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/views/MainWindow.xaml.cs
// 📌 Amac: MainWindow View eventlerini Controller katmanina aktarir ve startup resume requestini bir kez baslatir
// 📌 Modul - View CSharp
// Version: 1.7.0
// Aciklama: View code-behind is kurali tutmadan startup, DispatcherQueueTimer background check ve kullanici eventlerini Controller'a delege eder
//
// Bagimli Oldugu Katman: View | Controller

using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using TurkuazInstaller.Infrastructure.Branding;
using TurkuazInstaller.Domain.Products;
using System.ComponentModel;
using TurkuazInstaller.Presentation.Controllers;
using TurkuazInstaller.Presentation.Services;
using TurkuazInstaller.Presentation.ViewModels;
using TurkuazInstaller.WinUI.Tools;
using Windows.Graphics;

namespace TurkuazInstaller.WinUI.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowController _controller;
    private readonly InstallerDesktopService _desktopService;
    private readonly IReadOnlyList<string> _startupArguments;
    private DispatcherQueueTimer? _backgroundUpdateTimer;
    private bool _startupHandled;
    private readonly MainWindowViewModel _viewModel;
    private readonly IVerifiedThemePackReader? _themeReader;
    private readonly PackageId? _themePackageId;
    private readonly string? _themePath;
    private VerifiedThemePack? _verifiedTheme;
    private readonly Dictionary<string, BitmapImage> _themeImages = new(StringComparer.Ordinal);
    private bool _themeBlocked;


    public MainWindow(
        MainWindowViewModel viewModel,
        MainWindowController controller,
        InstallerDesktopService desktopService,
        IReadOnlyList<string> startupArguments,
        IVerifiedThemePackReader? themeReader = null,
        PackageId? themePackageId = null,
        string? themePath = null,
        bool lockedProductId = false)
    {
        WinUiStartupDiagnostics.Record("MainWindowConstructorEntered");
        InitializeComponent();
        WinUiStartupDiagnostics.Record("MainWindowXamlInitialized");

        ArgumentNullException.ThrowIfNull(
            startupArguments);

        _viewModel = viewModel;
        _themeReader = themeReader;
        _themePackageId = themePackageId;
        _themePath = themePath;
        _viewModel.PropertyChanged += OnThemeViewModelPropertyChanged;

        _controller = controller;
        _desktopService = desktopService;
        _startupArguments =
            Array.AsReadOnly(
                startupArguments.ToArray());

        MainRoot.DataContext = viewModel;
        PackageIdEntry.IsReadOnly = lockedProductId;
        // A required theme may take time to verify. Prevent any user action
        // before the signature check and full PNG decode have completed.
        MainContent.IsEnabled = _themePackageId is null;
        Title = viewModel.WindowTitle;

        WinUiStartupDiagnostics.Record("MainWindowResizeStarting");
        ResizeWindow();
        WinUiStartupDiagnostics.Record("MainWindowResizeComplete");
        Activated += OnActivated;
        Closed += OnClosed;
    }

    private async void OnActivated(
        object sender,
        WindowActivatedEventArgs args)
    {
        if (_startupHandled)
        {
            return;
        }

        _startupHandled = true;
        WinUiStartupDiagnostics.Record("ActivatedHandlerEntered");
        try
        {
            if (!await LoadVerifiedThemeAsync().ConfigureAwait(true))
            {
                return; // No install/update actions may run on bad signed themes.
            }

            await _controller
                .StartAsync(
                    _startupArguments)
                .ConfigureAwait(true);

            WinUiStartupDiagnostics.Record("StartupControllerCompleted");
            await _controller
                .RunBackgroundUpdateCheckAsync()
                .ConfigureAwait(true);
            WinUiStartupDiagnostics.Record("BackgroundUpdateCheckCompleted");
            ConfigureBackgroundUpdateTimer();
            WinUiStartupDiagnostics.Record("MainWindowReady");
        }
        catch (Exception error)
        {
            WinUiStartupDiagnostics.Record("ActivatedHandlerFailed", error);
            throw;
        }
    }


    // A configured adjacent theme is a requirement, not a best-effort fallback.
    // No decoded image reaches the View until the complete archive is verified.
    private async Task<bool> LoadVerifiedThemeAsync()
    {
        if (_themePackageId is null) return true;

        try
        {
            if (_themeReader is null || _themePath is null)
            {
                throw new InvalidDataException("Signed theme binding is incomplete.");
            }

            // Path is relative to the executable, not process working directory.
            using var input = new FileStream(
                _themePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 81920, FileOptions.SequentialScan);

            var theme = await _themeReader.ReadAsync(
                input, _themePackageId, CancellationToken.None).ConfigureAwait(true);
            // Decode every signed image before allowing installer operations.
            // No images are extracted to writable temporary folders.
            foreach (var (id, bytes) in theme.Assets)
            {
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream))
                {
                    writer.WriteBytes(bytes.ToArray());
                    await writer.StoreAsync();
                    writer.DetachStream();
                }

                stream.Seek(0);
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                _themeImages.Add(id, bitmap);
            }

            _verifiedTheme = theme;
            ApplyVerifiedThemeForStage();
            MainContent.IsEnabled = true;
            WinUiStartupDiagnostics.Record("SignedThemePackVerifiedAndDecoded");
            return true;
        }
        catch (Exception error)
        {
            _themeBlocked = true;
            MainContent.IsEnabled = false;
            ThemeVerificationFailure.IsOpen = true;
            WinUiStartupDiagnostics.Record("SignedThemePackRejected", error);
            return false;
        }
    }

    private void OnThemeViewModelPropertyChanged(
        object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainWindowViewModel.IsBusy)
            or nameof(MainWindowViewModel.HasError)
            or nameof(MainWindowViewModel.ProgressValue)
            or nameof(MainWindowViewModel.PackageIdText))
        {
            ApplyVerifiedThemeForStage();
        }
    }

    private void ApplyVerifiedThemeForStage()
    {
        if (_themeBlocked || _verifiedTheme is null) return;

        // A theme for one package must not be presented as another product.
        var currentProduct = _viewModel.PackageIdText.Trim();
        if (currentProduct.Length > 0 &&
            !string.Equals(currentProduct, _themePackageId?.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            VerifiedThemeBackdrop.Visibility = Visibility.Collapsed;
            VerifiedThemeContrastShade.Visibility = Visibility.Collapsed;
            return;
        }

        var stage = _viewModel.HasError ? "failed" :
            _viewModel.IsBusy ? "installing" :
            _viewModel.ProgressValue >= 100 ? "complete" :
            currentProduct.Length > 0 ? "options" : "welcome";

        if (!_verifiedTheme.Screens.TryGetValue(stage, out var id) ||
            !_themeImages.TryGetValue(id, out var image))
        {
            // Reuse only a verified welcome image, never untrusted disk art.
            if (!_verifiedTheme.Screens.TryGetValue("welcome", out id) ||
                !_themeImages.TryGetValue(id, out image))
            {
                VerifiedThemeBackdrop.Visibility = Visibility.Collapsed;
                VerifiedThemeContrastShade.Visibility = Visibility.Collapsed;
                return;
            }
        }

        VerifiedThemeBackdrop.Source = image;
        VerifiedThemeBackdrop.Visibility = Visibility.Visible;
        VerifiedThemeContrastShade.Visibility = Visibility.Visible;
    }

    private void ConfigureBackgroundUpdateTimer()
    {
        if (_backgroundUpdateTimer is not null)
        {
            return;
        }

        var interval =
            _controller
                .BackgroundUpdateInterval;

        if (interval is null)
        {
            return;
        }

        var dispatcherQueue =
            DispatcherQueue
                .GetForCurrentThread()
            ?? throw new InvalidOperationException(
                "WinUI DispatcherQueue is unavailable.");

        _backgroundUpdateTimer =
            dispatcherQueue
                .CreateTimer();

        _backgroundUpdateTimer.Interval =
            interval.Value;

        _backgroundUpdateTimer.IsRepeating =
            true;

        _backgroundUpdateTimer.Tick +=
            OnBackgroundUpdateTimerTick;

        _backgroundUpdateTimer.Start();
    }

    private async void OnBackgroundUpdateTimerTick(
        DispatcherQueueTimer sender,
        object args)
    {
        await _controller
            .RunBackgroundUpdateCheckAsync()
            .ConfigureAwait(true);
    }

    private async void RefreshInstalledApps_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _controller
            .RefreshInstalledAppsAsync()
            .ConfigureAwait(true);
    }

    private async void CheckUpdates_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _controller
            .CheckForUpdatesAsync()
            .ConfigureAwait(true);
    }

    private async void Install_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _controller
            .InstallAsync()
            .ConfigureAwait(true);
    }

    private async void Update_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _controller
            .UpdateAsync()
            .ConfigureAwait(true);
    }

    private async void Repair_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _controller
            .RepairAsync()
            .ConfigureAwait(true);
    }

    private async void Rollback_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _controller
            .RollbackAsync()
            .ConfigureAwait(true);
    }

    private async void Uninstall_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _controller
            .UninstallAsync()
            .ConfigureAwait(true);
    }

    private async void Retry_Click(
        object sender,
        RoutedEventArgs e)
    {
        await _controller
            .RetryAsync()
            .ConfigureAwait(true);
    }

    private void Cancel_Click(
        object sender,
        RoutedEventArgs e)
    {
        _controller.Cancel();
    }

    private void OnClosed(
        object sender,
        WindowEventArgs args)
    {
        if (_backgroundUpdateTimer is not null)
        {
            _backgroundUpdateTimer.Stop();
            _backgroundUpdateTimer.Tick -=
                OnBackgroundUpdateTimerTick;
            _backgroundUpdateTimer = null;
        }

        _viewModel.PropertyChanged -= OnThemeViewModelPropertyChanged;
        _themeImages.Clear();
        _desktopService.Dispose();
    }

    private void ResizeWindow()
    {
        var windowHandle =
            WinRT.Interop.WindowNative
                .GetWindowHandle(this);

        var windowId =
            Microsoft.UI.Win32Interop
                .GetWindowIdFromWindow(
                    windowHandle);

        var appWindow =
            AppWindow.GetFromWindowId(
                windowId);

        appWindow.Resize(
            new SizeInt32(
                1100,
                920));
    }
}
