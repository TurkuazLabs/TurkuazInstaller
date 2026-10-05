// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopPathDefaults.cs
// 📌 Amac: WinUI desktop runtime storage ve manifest trust store yollarini merkezi config katmaninda uretir
// 📌 Modul - Config CSharp
// Version: 1.3.0
// Aciklama: Local storage, manifest trust ve combined distribution reboot resume yollarini merkezi configte uretir
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.WinUI.Config;

internal static class DesktopPathDefaults
{
    private const string ProductDirectory =
        "TurkuazInstaller";

    private const string StateDirectory =
        "state";

    private const string StagingDirectory =
        "staging";

    private const string LockDirectory =
        "locks";

    private const string JournalDirectory =
        "journal";

    private const string LogDirectory =
        "logs";

    private const string ResumeDirectory =
        "resume";

    private const string ConfigDirectory =
        "config";

    private const string ManifestTrustFileName =
        "manifest-trust.yml";

    private const string BootstrapExecutableName =
        "TurkuazInstaller.Bootstrapper.exe";

    private const string RebootResumeValueNamePrefix =
        "TurkuazInstaller.Resume";

    private const string AppsDirectory =
        "TurkuazApps";

    public static DesktopRuntimeOptions CreateRuntimeOptions()
    {
        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var productRoot =
            Path.Combine(
                localAppData,
                ProductDirectory);

        return new DesktopRuntimeOptions(
            Path.Combine(
                productRoot,
                StateDirectory),
            Path.Combine(
                productRoot,
                StagingDirectory),
            Path.Combine(
                productRoot,
                LockDirectory),
            Path.Combine(
                productRoot,
                JournalDirectory),
            Path.Combine(
                productRoot,
                LogDirectory),
            Path.Combine(
                productRoot,
                ResumeDirectory),
            Path.Combine(
                productRoot,
                ConfigDirectory,
                ManifestTrustFileName),
            CreateBootstrapExecutablePath(),
            RebootResumeValueNamePrefix);
    }

    private static string CreateBootstrapExecutablePath()
    {
        var applicationRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    AppContext.BaseDirectory));

        var distributionRoot =
            Directory.GetParent(
                applicationRoot)
            ?.FullName
            ?? throw new InvalidOperationException(
                "Combined distribution root could not be resolved.");

        return Path.Combine(
            distributionRoot,
            BootstrapExecutableName);
    }

    public static string CreateDefaultInstallRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            AppsDirectory);
    }
}
