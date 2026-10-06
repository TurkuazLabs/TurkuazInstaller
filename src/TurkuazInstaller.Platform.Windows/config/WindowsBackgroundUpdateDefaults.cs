// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/config/WindowsBackgroundUpdateDefaults.cs
// 📌 Amac: WinUI session background update policy config yolunu merkezi olarak uretir
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Policy dosyasini LocalAppData altindaki ortak TurkuazInstaller config kokunde tutar
//
// Bagimli Oldugu Katman: Config | Tool

namespace TurkuazInstaller.Platform.Windows.Config;

public static class WindowsBackgroundUpdateDefaults
{
    private const string ProductDirectory =
        "TurkuazInstaller";

    private const string ConfigDirectory =
        "config";

    private const string FileName =
        "background-updates.json";

    public static string CreateConfigPath()
    {
        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localAppData,
            ProductDirectory,
            ConfigDirectory,
            FileName);
    }
}
