// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/views/MainWindow.xaml.cs
// 📌 Amac: MainWindow View eventlerini Controller katmanina aktarir ve startup resume requestini bir kez baslatir
// 📌 Modul - View CSharp
// Version: 1.2.0
// Aciklama: View code-behind is kurali tutmadan startup/catalog refresh/install/update/repair/rollback/uninstall eventlerini Controller'a delege eder
//
// Bagimli Oldugu Katman: View | Controller

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TurkuazInstaller.Presentation.Controllers;
using TurkuazInstaller.Presentation.Services;
using TurkuazInstaller.Presentation.ViewModels;
using Windows.Graphics;

namespace TurkuazInstaller.WinUI.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowController _controller;
    private readonly InstallerDesktopService _desktopService;
    private readonly IReadOnlyList<string> _startupArguments;
    private bool _startupHandled;

    public MainWindow(
        MainWindowViewModel viewModel,
        MainWindowController controller,
        InstallerDesktopService desktopService,
        IReadOnlyList<string> startupArguments)
    {
        InitializeComponent();

        ArgumentNullException.ThrowIfNull(
            startupArguments);

        _controller = controller;
        _desktopService = desktopService;
        _startupArguments =
            Array.AsReadOnly(
                startupArguments.ToArray());

        MainRoot.DataContext = viewModel;
        Title = viewModel.WindowTitle;

        ResizeWindow();
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

        await _controller
            .StartAsync(
                _startupArguments)
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
