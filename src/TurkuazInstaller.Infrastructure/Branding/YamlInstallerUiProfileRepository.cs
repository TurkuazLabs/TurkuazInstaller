// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Branding/YamlInstallerUiProfileRepository.cs
// 📌 Amac: Local YAML UI profilini okuyup typed branding/localization contractina donusturur
// 📌 Modul - Repo CSharp
// Version: 1.1.0
// Aciklama: Schema, culture, branding ve tum typed label anahtarlarini enumdan uretilen contract isimleriyle fail-closed dogrular
//
// Bagimli Oldugu Katman: Repo | Tool | Language

using System.Globalization;
using TurkuazInstaller.Contracts.Branding;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TurkuazInstaller.Infrastructure.Branding;

public sealed class YamlInstallerUiProfileRepository
    : IInstallerUiProfileRepository
{
    private const int SupportedSchemaVersion = 1;

    private static readonly IReadOnlyDictionary<string, InstallerUiLabelKey>
        SupportedLabelKeys =
            Enum.GetValues<InstallerUiLabelKey>()
                .ToDictionary(
                    ToContractKey,
                    key => key,
                    StringComparer.Ordinal);

    private readonly string _path;
    private readonly IDeserializer _deserializer;

    public YamlInstallerUiProfileRepository(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        _path = Path.GetFullPath(path);
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(
                UnderscoredNamingConvention.Instance)
            .Build();
    }

    public InstallerUiProfile Read()
    {
        if (!File.Exists(_path))
        {
            return InstallerUiProfile.Empty;
        }

        var yaml =
            File.ReadAllText(_path);

        var document =
            _deserializer.Deserialize<UiProfileDocument>(yaml)
            ?? throw new FormatException(
                "Installer UI profile is empty.");

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new FormatException(
                "Installer UI profile schema version is not supported.");
        }

        var culture =
            NormalizeCulture(
                document.Culture);

        var labels =
            ParseLabels(
                document.Labels);

        return new InstallerUiProfile(
            culture,
            new InstallerBrandingProfile(
                NormalizeOptional(
                    document.Branding.WindowTitle),
                NormalizeOptional(
                    document.Branding.HeaderTitle),
                NormalizeOptional(
                    document.Branding.HeaderSubtitle),
                NormalizeOptional(
                    document.Branding.Footer)),
            labels);
    }

    private static string? NormalizeCulture(
        string? value)
    {
        var normalized =
            NormalizeOptional(value);

        if (normalized is null)
        {
            return null;
        }

        try
        {
            var culture =
                CultureInfo.GetCultureInfo(
                    normalized);

            var isKnownCulture =
                CultureInfo
                    .GetCultures(
                        CultureTypes.AllCultures)
                    .Any(
                        candidate =>
                            string.Equals(
                                candidate.Name,
                                culture.Name,
                                StringComparison.OrdinalIgnoreCase));

            if (!isKnownCulture)
            {
                throw new FormatException(
                    "Installer UI profile culture is not supported.");
            }

            return culture.Name;
        }
        catch (CultureNotFoundException exception)
        {
            throw new FormatException(
                "Installer UI profile culture is not supported.",
                exception);
        }
    }

    private static IReadOnlyDictionary<InstallerUiLabelKey, string>
        ParseLabels(
            IReadOnlyDictionary<string, string>? labels)
    {
        if (labels is null || labels.Count == 0)
        {
            return new Dictionary<InstallerUiLabelKey, string>();
        }

        var result =
            new Dictionary<InstallerUiLabelKey, string>();

        foreach (var pair in labels)
        {
            if (!SupportedLabelKeys.TryGetValue(
                    pair.Key,
                    out var labelKey))
            {
                throw new FormatException(
                    $"Installer UI profile label key is not supported: {pair.Key}");
            }

            var value =
                NormalizeOptional(
                    pair.Value)
                ?? throw new FormatException(
                    $"Installer UI profile label value is empty: {pair.Key}");

            result[labelKey] = value;
        }

        return result;
    }

    private static string ToContractKey(
        InstallerUiLabelKey key)
    {
        var name =
            key.ToString();

        var builder =
            new System.Text.StringBuilder(
                name.Length + 8);

        for (var index = 0;
             index < name.Length;
             index++)
        {
            var character =
                name[index];

            if (
                index > 0 &&
                char.IsUpper(character))
            {
                builder.Append(
                    '_');
            }

            builder.Append(
                char.ToLowerInvariant(
                    character));
        }

        return builder.ToString();
    }

    private static string? NormalizeOptional(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed class UiProfileDocument
    {
        public int SchemaVersion { get; init; }

        public string? Culture { get; init; }

        public BrandingSection Branding { get; init; } =
            new();

        public Dictionary<string, string> Labels { get; init; } =
            new(StringComparer.Ordinal);
    }

    private sealed class BrandingSection
    {
        public string? WindowTitle { get; init; }

        public string? HeaderTitle { get; init; }

        public string? HeaderSubtitle { get; init; }

        public string? Footer { get; init; }
    }
}
