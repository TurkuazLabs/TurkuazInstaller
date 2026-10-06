// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/config/WindowsVersionUpdatePolicyDefaults.cs
// 📌 Amac: Version skip/pinning policy config yolunu merkezi olarak uretir
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: WinUI ve CLI'nin ayni LocalAppData version-policy.json dosyasini kullanmasini saglar
//
// Bagimli Oldugu Katman: Config | Repo

namespace TurkuazInstaller.Platform.Windows.Config;

public static class WindowsVersionUpdatePolicyDefaults
{
    private const string ProductDirectory =
        "TurkuazInstaller";

    private const string ConfigDirectory =
        "config";

    private const string FileName =
        "version-policy.json";

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
