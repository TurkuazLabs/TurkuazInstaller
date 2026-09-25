// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Configuration/InstallerFileLayout.cs
// 📌 Amac: Installer runtime dosya yerlesim sozlesmesini tek noktada tanimlar
// 📌 Modul - Infrastructure CSharp
// Version: 0.1.0
// Aciklama: Config dosyasi ve language uzantisi gibi yapisal path sabitlerini merkezilestirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Infrastructure.Configuration;

public static class InstallerFileLayout
{
    public const string ConfigDirectory = "config";
    public const string InstallerConfigurationFile = "installer.yml";
    public const string LanguageFileExtension = ".yml";
}
