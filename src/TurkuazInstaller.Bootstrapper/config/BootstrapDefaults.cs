// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/config/BootstrapDefaults.cs
// 📌 Amac: Native bootstrap runtime requirement ve self-update retry varsayilanlarini tek config katmaninda tanimlar
// 📌 Modul - Config CSharp
// Version: 0.6.0
// Aciklama: Windows 10 1809 tabani, desteklenen mimariler ve handoff retry politikasini inline config disina tasir
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
                BootstrapCpuArchitecture.X64,
                BootstrapCpuArchitecture.Arm64
            });
    }

    public static SelfUpdateHandoffOptions CreateSelfUpdateOptions()
    {
        return new SelfUpdateHandoffOptions(
            ReplacementAttempts,
            TimeSpan.FromMilliseconds(
                ReplacementRetryMilliseconds));
    }
}
