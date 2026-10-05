// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsRunOnceRebootResumeScheduler.cs
// 📌 Amac: Reboot isteyen prerequisite oncesinde bootstrap resume komutunu kullanici RunOnce kaydina yazar
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Package-scoped !RunOnce kaydi ile bootstrap'i internal resume argumaniyla tek seferlik yeniden baslatir
//
// Bagimli Oldugu Katman: Tool | Service | Config

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsRunOnceRebootResumeScheduler
    : IRebootResumeScheduler
{
    private const int MaximumRunOnceCommandLength = 260;
    private const string DeferredDeletionPrefix = "!";

    private readonly IWindowsRunOnceStore _store;
    private readonly WindowsRunOnceRebootResumeSchedulerOptions _options;

    public WindowsRunOnceRebootResumeScheduler(
        IWindowsRunOnceStore store,
        WindowsRunOnceRebootResumeSchedulerOptions options)
    {
        _store = store;
        _options = options;
    }

    public Task ScheduleAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(
                _options.BootstrapExecutablePath))
        {
            throw new FileNotFoundException(
                "Bootstrap executable required for reboot resume was not found.",
                _options.BootstrapExecutablePath);
        }

        var commandLine =
            BuildCommandLine(
                _options.BootstrapExecutablePath,
                packageId);

        if (
            commandLine.Length >
            MaximumRunOnceCommandLength)
        {
            throw new InvalidOperationException(
                "Reboot resume RunOnce command exceeds the Windows command length limit.");
        }

        _store.Set(
            BuildValueName(
                packageId),
            commandLine);

        return Task.CompletedTask;
    }

    public Task CancelAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        cancellationToken.ThrowIfCancellationRequested();

        _store.Delete(
            BuildValueName(
                packageId));

        return Task.CompletedTask;
    }

    private string BuildValueName(
        PackageId packageId)
    {
        return string.Concat(
            DeferredDeletionPrefix,
            _options.ValueNamePrefix,
            ".",
            packageId.Value);
    }

    private static string BuildCommandLine(
        string bootstrapExecutablePath,
        PackageId packageId)
    {
        return string.Concat(
            "\"",
            bootstrapExecutablePath,
            "\" ",
            InstallerResumeLaunchArguments.PackageOption,
            " ",
            packageId.Value);
    }
}
