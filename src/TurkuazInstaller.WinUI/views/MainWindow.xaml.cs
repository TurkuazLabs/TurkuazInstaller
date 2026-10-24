// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/views/MainWindow.xaml.cs
// 📌 Amac: MainWindow View eventlerini Controller katmanina aktarir ve startup resume requestini bir kez baslatir
// 📌 Modul - View CSharp
// Version: 1.5.0
// Aciklama: View code-behind is kurali tutmadan startup, DispatcherQueueTimer background check ve kullanici eventlerini Controller'a delege eder
//
// Bagimli Oldugu Katman: View | Controller

using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TurkuazInstaller.WinUI.Services;
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

    public MainWindow(
        MainWindowViewModel viewModel,
        MainWindowController controller,
        InstallerDesktopService desktopService,
        IReadOnlyList<string> startupArguments)
    {
        WinUiStartupDiagnostics.Record("MainWindowConstructorEntered");
        InitializeComponent();
        WinUiStartupDiagnostics.Record("MainWindowXamlInitialized");

        ArgumentNullException.ThrowIfNull(
            startupArguments);

        _controller = controller;
        _desktopService = desktopService;
        _startupArguments =
            Array.AsReadOnly(
                startupArguments.ToArray());

        MainRoot.DataContext = viewModel;
        Title = viewModel.WindowTitle;
        ThemeSelector.SelectionChanged += OnThemeSelectionChanged;
        ThemeSelector.SelectedIndex = ThemeSelectionStore.Read() switch
        {
            "dark" => 1,
            "light" => 2,
            _ => 0
        };

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

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ThemeSelector.SelectedItem is not ComboBoxItem selected)
        {
            return;
        }

        var preset = selected.Tag?.ToString() ?? "system";
        if (!ThemeSelectionStore.IsValid(preset))
        {
            return;
        }

        MainRoot.RequestedTheme = preset switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        ThemeSelectionStore.TryWrite(preset);
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

        ThemeSelector.SelectionChanged -= OnThemeSelectionChanged;
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
