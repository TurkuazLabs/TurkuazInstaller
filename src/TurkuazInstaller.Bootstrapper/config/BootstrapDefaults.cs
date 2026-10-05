// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/config/BootstrapDefaults.cs
// 📌 Amac: Native bootstrap runtime requirement, app layout ve self-update retry varsayilanlarini tek config katmaninda tanimlar
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Windows baseline, x64 Stable v1 policy, combined distribution app yolu ve handoff retry politikasini merkezilestirir
//
// Bagimli Oldugu Katman: Config

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Contracts.Bootstrap;
using TurkuazInstaller.Platform.Windows.Tools;

namespace TurkuazInstaller.Bootstrapper.Config;

internal static class BootstrapDefaults
{
    private const int Windows10Build1809 = 17763;
    private const int ReplacementAttempts = 12;
    private const int ReplacementRetryMilliseconds = 250;

    private const string AppDirectoryName = "app";
    private const string DesktopExecutableName =
        "TurkuazInstaller.WinUI.exe";

    public static BootstrapRequirements CreateRequirements()
    {
        return new BootstrapRequirements(
            new Version(
                10,
                0,
                Windows10Build1809,
                0),
            new[]
            {
                BootstrapCpuArchitecture.X64
            });
    }

    public static SelfUpdateHandoffOptions CreateSelfUpdateOptions()
    {
        return new SelfUpdateHandoffOptions(
            ReplacementAttempts,
            TimeSpan.FromMilliseconds(
                ReplacementRetryMilliseconds));
    }

    public static BootstrapRuntimeOptions CreateRuntimeOptions()
    {
        return new BootstrapRuntimeOptions(
            Path.Combine(
                AppDirectoryName,
                DesktopExecutableName));
    }
}
