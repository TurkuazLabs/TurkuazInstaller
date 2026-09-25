// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Configuration/YamlInstallerUiConfigurationLoader.cs
// 📌 Amac: Installer UI konfigurasyonunu YAML dosyasindan typed modele yukler
// 📌 Modul - Infrastructure CSharp
// Version: 0.1.0
// Aciklama: App composition root icindeki inline runtime config ihtiyacini kaldirir
//
// Bagimli Oldugu Katman: Tool

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TurkuazInstaller.Infrastructure.Configuration;

public sealed class YamlInstallerUiConfigurationLoader
{
    public InstallerUiConfiguration Load(string filePath)
    {
        using var reader = File.OpenText(filePath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var document = deserializer.Deserialize<ConfigurationDocument>(reader);

        return new InstallerUiConfiguration(
            document.DefaultLanguage,
            document.LanguageDirectory);
    }

    private sealed class ConfigurationDocument
    {
        public string DefaultLanguage { get; init; } = string.Empty;

        public string LanguageDirectory { get; init; } = string.Empty;
    }
}
