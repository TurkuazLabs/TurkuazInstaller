// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/views/MainWindow.xaml.cs
// 📌 Amac: MainWindow View eventlerini Controller katmanina aktarir ve pencere gorunumunu baslatir
// 📌 Modul - View CSharp
// Version: 1.0.0
// Aciklama: View code-behind is kurali tutmadan install/update/repair/rollback/uninstall eventlerini Controller'a delege eder
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

    public MainWindow(
        MainWindowViewModel viewModel,
        MainWindowController controller,
        InstallerDesktopService desktopService)
    {
        InitializeComponent();

        _controller = controller;
        _desktopService = desktopService;

        MainRoot.DataContext = viewModel;
        Title = viewModel.WindowTitle;

        ResizeWindow();
        Closed += OnClosed;
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
                800));
    }
}
