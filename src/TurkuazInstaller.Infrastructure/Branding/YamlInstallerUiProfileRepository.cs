// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Branding/YamlInstallerUiProfileRepository.cs
// 📌 Amac: Local YAML UI profilini okuyup typed branding/localization contractina donusturur
// 📌 Modul - Repo CSharp
// Version: 1.0.0
// Aciklama: Schema, culture, branding ve desteklenen label anahtarlarini fail-closed dogrular
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

    private const string PackageIdKey = "package_id";
    private const string ChannelKey = "channel";
    private const string ManifestSourceKey = "manifest_source";
    private const string RollbackManifestSourceKey = "rollback_manifest_source";
    private const string TargetPathKey = "target_path";
    private const string InstallKey = "install";
    private const string UpdateKey = "update";
    private const string RepairKey = "repair";
    private const string RollbackKey = "rollback";
    private const string UninstallKey = "uninstall";
    private const string RetryKey = "retry";
    private const string CancelKey = "cancel";
    private const string RefreshInstalledAppsKey = "refresh_installed_apps";
    private const string CheckUpdatesKey = "check_updates";
    private const string UpdateDiscoveryTitleKey = "update_discovery_title";
    private const string BackgroundUpdatesTitleKey = "background_updates_title";
    private const string InstalledAppsTitleKey = "installed_apps_title";
    private const string StatusTitleKey = "status_title";
    private const string SourceTitleKey = "source_title";
    private const string OperationsTitleKey = "operations_title";
    private const string RecoveryTitleKey = "recovery_title";

    private static readonly IReadOnlyDictionary<string, InstallerUiLabelKey>
        SupportedLabelKeys =
            new Dictionary<string, InstallerUiLabelKey>(
                StringComparer.Ordinal)
            {
                [PackageIdKey] = InstallerUiLabelKey.PackageId,
                [ChannelKey] = InstallerUiLabelKey.Channel,
                [ManifestSourceKey] = InstallerUiLabelKey.ManifestSource,
                [RollbackManifestSourceKey] = InstallerUiLabelKey.RollbackManifestSource,
                [TargetPathKey] = InstallerUiLabelKey.TargetPath,
                [InstallKey] = InstallerUiLabelKey.Install,
                [UpdateKey] = InstallerUiLabelKey.Update,
                [RepairKey] = InstallerUiLabelKey.Repair,
                [RollbackKey] = InstallerUiLabelKey.Rollback,
                [UninstallKey] = InstallerUiLabelKey.Uninstall,
                [RetryKey] = InstallerUiLabelKey.Retry,
                [CancelKey] = InstallerUiLabelKey.Cancel,
                [RefreshInstalledAppsKey] = InstallerUiLabelKey.RefreshInstalledApps,
                [CheckUpdatesKey] = InstallerUiLabelKey.CheckUpdates,
                [UpdateDiscoveryTitleKey] = InstallerUiLabelKey.UpdateDiscoveryTitle,
                [BackgroundUpdatesTitleKey] = InstallerUiLabelKey.BackgroundUpdatesTitle,
                [InstalledAppsTitleKey] = InstallerUiLabelKey.InstalledAppsTitle,
                [StatusTitleKey] = InstallerUiLabelKey.StatusTitle,
                [SourceTitleKey] = InstallerUiLabelKey.SourceTitle,
                [OperationsTitleKey] = InstallerUiLabelKey.OperationsTitle,
                [RecoveryTitleKey] = InstallerUiLabelKey.RecoveryTitle
            };

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

        return CultureInfo
            .GetCultureInfo(normalized)
            .Name;
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
