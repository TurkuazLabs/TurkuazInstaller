// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopPathDefaults.cs
// 📌 Amac: WinUI desktop runtime storage ve manifest trust store yollarini merkezi config katmaninda uretir
// 📌 Modul - Config CSharp
// Version: 1.2.0
// Aciklama: LocalApplicationData altindaki state, staging, lock, journal, log ve external manifest trust config politikasini inline path stringlerinden ayirir
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

    private const string ConfigDirectory =
        "config";

    private const string ManifestTrustFileName =
        "manifest-trust.yml";

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
                ConfigDirectory,
                ManifestTrustFileName));
    }

    public static string CreateDefaultInstallRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            AppsDirectory);
    }
}
