// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/config/WindowsNetworkProxyDefaults.cs
// 📌 Amac: Bootstrap, WinUI ve CLI icin ortak network proxy config yolunu merkezi olarak uretir
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Tum Windows runtime'larinin ayni LocalAppData network.json politikasini okumasini saglar
//
// Bagimli Oldugu Katman: Config | Tool

namespace TurkuazInstaller.Platform.Windows.Config;

public static class WindowsNetworkProxyDefaults
{
    private const string ProductDirectory =
        "TurkuazInstaller";

    private const string ConfigDirectory =
        "config";

    private const string NetworkConfigFileName =
        "network.json";

    public static string CreateConfigPath()
    {
        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localAppData,
            ProductDirectory,
            ConfigDirectory,
            NetworkConfigFileName);
    }
}
