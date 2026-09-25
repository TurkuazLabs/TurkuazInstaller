// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Localization/YamlTextCatalog.cs
// 📌 Amac: UI metinlerini YAML language dosyasindan okur
// 📌 Modul - Infrastructure CSharp
// Version: 0.1.0
// Aciklama: Kullanici metinlerini ViewModel ve View kodundan ayirir
//
// Bagimli Oldugu Katman: Language

using TurkuazInstaller.Contracts.Localization;
using YamlDotNet.Serialization;

namespace TurkuazInstaller.Infrastructure.Localization;

public sealed class YamlTextCatalog : ITextCatalog
{
    private const string MissingKeyPrefix = "missing_language_key:";

    private readonly IReadOnlyDictionary<string, string> _values;

    public YamlTextCatalog(string filePath)
    {
        using var reader = File.OpenText(filePath);
        var deserializer = new DeserializerBuilder().Build();
        _values = deserializer.Deserialize<Dictionary<string, string>>(reader);
    }

    public string Get(string key)
    {
        return _values.TryGetValue(key, out var value)
            ? value
            : string.Concat(MissingKeyPrefix, key);
    }
}
