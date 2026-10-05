// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsRunOnceRebootResumeSchedulerOptions.cs
// 📌 Amac: Reboot resume RunOnce scheduler bootstrap yolu ve value prefix ayarlarini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Windows scheduler path ve registry value naming politikasini Tool implementasyonundan ayirir
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed record WindowsRunOnceRebootResumeSchedulerOptions
{
    public WindowsRunOnceRebootResumeSchedulerOptions(
        string bootstrapExecutablePath,
        string valueNamePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            bootstrapExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            valueNamePrefix);

        BootstrapExecutablePath =
            Path.GetFullPath(
                bootstrapExecutablePath);

        ValueNamePrefix =
            valueNamePrefix.Trim();
    }

    public string BootstrapExecutablePath { get; }

    public string ValueNamePrefix { get; }
}
