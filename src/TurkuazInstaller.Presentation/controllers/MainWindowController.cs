// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/controllers/MainWindowController.cs
// 📌 Amac: WinUI View event requestlerini alir ve yalniz InstallerDesktopService cagrisina donusturur
// 📌 Modul - Controller CSharp
// Version: 1.3.0
// Aciklama: Startup, catalog refresh, read-only update discovery, mutation, cancel ve retry requestlerini Service katmanina aktarir
//
// Bagimli Oldugu Katman: Controller | Service

using TurkuazInstaller.Presentation.Services;
using TurkuazInstaller.Presentation.ViewModels;

namespace TurkuazInstaller.Presentation.Controllers;

public sealed class MainWindowController
{
    private readonly InstallerDesktopService _service;

    public MainWindowController(
        InstallerDesktopService service)
    {
        _service = service;
    }

    public Task StartAsync(
        IReadOnlyList<string> launchArguments) =>
        _service.StartAsync(
            launchArguments);

    public Task InstallAsync() =>
        _service.RunAsync(
            InstallerOperationKind.Install);

    public Task UpdateAsync() =>
        _service.RunAsync(
            InstallerOperationKind.Update);

    public Task RepairAsync() =>
        _service.RunAsync(
            InstallerOperationKind.Repair);

    public Task RollbackAsync() =>
        _service.RunAsync(
            InstallerOperationKind.Rollback);

    public Task UninstallAsync() =>
        _service.RunAsync(
            InstallerOperationKind.Uninstall);

    public Task CheckForUpdatesAsync() =>
        _service.CheckForUpdatesAsync();

    public Task RefreshInstalledAppsAsync() =>
        _service.RefreshInstalledAppsAsync();

    public Task RetryAsync() =>
        _service.RetryAsync();

    public void Cancel() =>
        _service.Cancel();
}
