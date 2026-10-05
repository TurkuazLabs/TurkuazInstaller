// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopPathDefaults.cs
// 📌 Amac: WinUI desktop runtime varsayilan state, staging, lock ve install root yollarini merkezi config katmaninda uretir
// 📌 Modul - Config CSharp
// Version: 1.1.0
// Aciklama: LocalApplicationData altindaki TurkuazInstaller storage politikasini inline path stringlerinden ayirir
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.WinUI.Config;

internal static class DesktopPathDefaults
{
    private const string ProductDirectory = "TurkuazInstaller";
    private const string StateDirectory = "state";
    private const string StagingDirectory = "staging";
    private const string LockDirectory = "locks";
    private const string AppsDirectory = "TurkuazApps";

    public static DesktopRuntimeOptions CreateRuntimeOptions()
    {
        var localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        var productRoot = Path.Combine(
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
                LockDirectory));
    }

    public static string CreateDefaultInstallRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            AppsDirectory);
    }
}
