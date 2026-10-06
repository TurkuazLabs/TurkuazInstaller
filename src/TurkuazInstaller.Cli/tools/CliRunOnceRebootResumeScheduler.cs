// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/tools/CliRunOnceRebootResumeScheduler.cs
// 📌 Amac: Silent CLI operasyonlarini reboot sonrasinda ayni CLI executable ile tek seferlik yeniden baslatir
// 📌 Modul - Tool CSharp
// Version: 1.0.1
// Aciklama: Package-scoped HKCU RunOnce komutunu --resume-package ve --silent protokoluyle olusturur
//
// Bagimli Oldugu Katman: Tool | Service | Config

using TurkuazInstaller.Cli.Config;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Platform.Windows.Tools;

namespace TurkuazInstaller.Cli.Tools;

internal sealed class CliRunOnceRebootResumeScheduler
    : IRebootResumeScheduler
{
    private const int MaximumRunOnceCommandLength = 260;
    private const string DeferredDeletionPrefix = "!";

    private readonly IWindowsRunOnceStore _store;
    private readonly string _executablePath;
    private readonly string _valueNamePrefix;

    public CliRunOnceRebootResumeScheduler(
        IWindowsRunOnceStore store,
        string executablePath,
        string valueNamePrefix)
    {
        _store = store;
        _executablePath =
            Path.GetFullPath(
                executablePath);
        _valueNamePrefix =
            string.IsNullOrWhiteSpace(valueNamePrefix)
                ? throw new ArgumentException(
                    "RunOnce value name prefix is required.",
                    nameof(valueNamePrefix))
                : valueNamePrefix.Trim();
    }

    public Task ScheduleAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_executablePath))
        {
            throw new FileNotFoundException(
                "CLI executable required for reboot resume was not found.",
                _executablePath);
        }

        var commandLine =
            string.Concat(
                "\"",
                _executablePath,
                "\" ",
                CliArguments.InternalResumePackageOption,
                " ",
                packageId.Value,
                " ",
                CliArguments.SilentOption);

        if (commandLine.Length > MaximumRunOnceCommandLength)
        {
            throw new InvalidOperationException(
                "CLI reboot resume RunOnce command exceeds the Windows command length limit.");
        }

        _store.Set(
            BuildValueName(packageId),
            commandLine);

        return Task.CompletedTask;
    }

    public Task CancelAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        cancellationToken.ThrowIfCancellationRequested();

        _store.Delete(
            BuildValueName(packageId));

        return Task.CompletedTask;
    }

    private string BuildValueName(
        PackageId packageId)
    {
        return string.Concat(
            DeferredDeletionPrefix,
            _valueNamePrefix,
            ".",
            packageId.Value);
    }
}
