// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsRegistryRunOnceStore.cs
// 📌 Amac: Kullanici bazli Windows RunOnce kaydini Registry uzerinden yazar ve temizler
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Reboot sonrasi tek seferlik bootstrap relaunch kaydini HKCU RunOnce altinda yonetir
//
// Bagimli Oldugu Katman: Tool

using Microsoft.Win32;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsRegistryRunOnceStore
    : IWindowsRunOnceStore
{
    private const string RunOnceRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\RunOnce";

    public void Set(
        string valueName,
        string commandLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            commandLine);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows RunOnce registry is only available on Windows.");
        }

        using var key =
            Registry.CurrentUser.CreateSubKey(
                RunOnceRegistryPath,
                writable: true)
            ?? throw new InvalidOperationException(
                "Windows RunOnce registry key could not be opened.");

        key.SetValue(
            valueName,
            commandLine,
            RegistryValueKind.String);
    }

    public void Delete(
        string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            valueName);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows RunOnce registry is only available on Windows.");
        }

        using var key =
            Registry.CurrentUser.OpenSubKey(
                RunOnceRegistryPath,
                writable: true);

        key?.DeleteValue(
            valueName,
            throwOnMissingValue: false);
    }
}
