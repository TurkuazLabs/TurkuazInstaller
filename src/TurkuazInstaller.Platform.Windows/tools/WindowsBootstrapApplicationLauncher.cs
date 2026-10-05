// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsBootstrapApplicationLauncher.cs
// 📌 Amac: Bootstrap prerequisite ve self-update akisindan sonra WinUI desktop executable dosyasini unelevated baslatir
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: UseShellExecute=false ve ArgumentList kullanarak app launch process sinirini uygular
//
// Bagimli Oldugu Katman: Tool | Service

using System.Diagnostics;
using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsBootstrapApplicationLauncher
    : IBootstrapApplicationLauncher
{
    public Task LaunchAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        cancellationToken.ThrowIfCancellationRequested();

        var fullPath =
            Path.GetFullPath(
                executablePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Bootstrap desktop application executable was not found.",
                fullPath);
        }

        var directory =
            Path.GetDirectoryName(
                fullPath)
            ?? throw new InvalidOperationException(
                "Bootstrap desktop application directory could not be resolved.");

        var startInfo =
            new ProcessStartInfo
            {
                FileName = fullPath,
                WorkingDirectory = directory,
                UseShellExecute = false,
                CreateNoWindow = false
            };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Bootstrap desktop application could not be started.");

        return Task.CompletedTask;
    }
}
