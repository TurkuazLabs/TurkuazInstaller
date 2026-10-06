// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsBackgroundUpdatePolicyReader.cs
// 📌 Amac: LocalAppData background-updates.json dosyasini strict typed session policy modeline donusturur
// 📌 Modul - Tool CSharp
// Version: 1.0.1
// Aciklama: Missing config disabled defaulttir; interval/entry/channel/source strict dogrulanir ve persistent HTTPS source secret-bearing URI parcalari reddedilir
//
// Bagimli Oldugu Katman: Tool | Config | Service

using System.Text.Json;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsBackgroundUpdatePolicyReader
{
    private const int SchemaVersion = 1;
    private const int MinimumIntervalMinutes = 15;
    private const int MaximumIntervalMinutes = 1440;
    private const int MaximumEntryCount = 100;
    private const int MaximumManifestSourceLength = 4096;

    private static readonly IReadOnlySet<string>
        AllowedRootProperties =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "schema_version",
                "enabled",
                "interval_minutes",
                "entries"
            };

    private static readonly IReadOnlySet<string>
        AllowedEntryProperties =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "package_id",
                "channel",
                "manifest_source"
            };

    private readonly string _configPath;

    public WindowsBackgroundUpdatePolicyReader(
        string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            configPath);

        _configPath =
            Path.GetFullPath(
                configPath);
    }

    public BackgroundUpdatePolicy Read()
    {
        if (!File.Exists(
                _configPath))
        {
            return BackgroundUpdatePolicy.Disabled();
        }

        using var stream =
            new FileStream(
                _configPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.SequentialScan);

        using var document =
            JsonDocument.Parse(
                stream,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling =
                        JsonCommentHandling.Disallow,
                    MaxDepth = 8
                });

        var root =
            document.RootElement;

        if (
            root.ValueKind !=
            JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Background update policy root must be a JSON object.");
        }

        ValidateProperties(
            root,
            AllowedRootProperties,
            "background update policy");

        var schemaVersion =
            GetRequiredInt32(
                root,
                "schema_version");

        if (schemaVersion != SchemaVersion)
        {
            throw new InvalidDataException(
                "Background update policy schema_version is unsupported.");
        }

        var enabled =
            GetRequiredBoolean(
                root,
                "enabled");

        var intervalMinutes =
            root.TryGetProperty(
                "interval_minutes",
                out var intervalElement)
                ? RequireInt32(
                    intervalElement,
                    "interval_minutes")
                : 60;

        if (
            intervalMinutes <
                MinimumIntervalMinutes ||
            intervalMinutes >
                MaximumIntervalMinutes)
        {
            throw new InvalidDataException(
                "Background update interval must be between 15 and 1440 minutes.");
        }

        var entries =
            ReadEntries(
                root);

        return new BackgroundUpdatePolicy(
            enabled,
            TimeSpan.FromMinutes(
                intervalMinutes),
            entries);
    }

    private static IReadOnlyList<BackgroundUpdatePolicyEntry>
        ReadEntries(
            JsonElement root)
    {
        if (
            !root.TryGetProperty(
                "entries",
                out var entriesElement))
        {
            return Array.Empty<BackgroundUpdatePolicyEntry>();
        }

        if (
            entriesElement.ValueKind !=
            JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Background update entries must be an array.");
        }

        if (
            entriesElement.GetArrayLength() >
            MaximumEntryCount)
        {
            throw new InvalidDataException(
                "Background update policy has too many entries.");
        }

        var entries =
            new List<BackgroundUpdatePolicyEntry>();

        var identities =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var entryElement in
                 entriesElement.EnumerateArray())
        {
            if (
                entryElement.ValueKind !=
                JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "Background update entry must be a JSON object.");
            }

            ValidateProperties(
                entryElement,
                AllowedEntryProperties,
                "background update entry");

            var packageId =
                PackageId.Parse(
                    GetRequiredString(
                        entryElement,
                        "package_id"));

            var channel =
                ParseChannel(
                    GetRequiredString(
                        entryElement,
                        "channel"));

            var manifestSource =
                ValidateManifestSource(
                    GetRequiredString(
                        entryElement,
                        "manifest_source"));

            var identity =
                string.Concat(
                    packageId.Value,
                    "|",
                    channel.ToString());

            if (!identities.Add(
                    identity))
            {
                throw new InvalidDataException(
                    "Background update policy contains duplicate package/channel entries.");
            }

            entries.Add(
                new BackgroundUpdatePolicyEntry(
                    packageId,
                    channel,
                    manifestSource));
        }

        return entries.AsReadOnly();
    }

    private static ReleaseChannel ParseChannel(
        string value)
    {
        return value.Trim()
            .ToLowerInvariant() switch
            {
                "stable" =>
                    ReleaseChannel.Stable,
                "beta" =>
                    ReleaseChannel.Beta,
                _ =>
                    throw new InvalidDataException(
                        "Background update entry channel must be stable or beta.")
            };
    }

    private static string ValidateManifestSource(
        string value)
    {
        var source =
            value.Trim();

        if (
            source.Length == 0 ||
            source.Length >
                MaximumManifestSourceLength)
        {
            throw new InvalidDataException(
                "Background update manifest source is invalid.");
        }

        if (
            Uri.TryCreate(
                source,
                UriKind.Absolute,
                out var uri))
        {
            if (
                string.Equals(
                    uri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (
                    !string.IsNullOrEmpty(
                        uri.UserInfo) ||
                    !string.IsNullOrEmpty(
                        uri.Query) ||
                    !string.IsNullOrEmpty(
                        uri.Fragment))
                {
                    throw new InvalidDataException(
                        "Background update HTTPS manifest source must not contain userinfo, query or fragment.");
                }

                return source;
            }

            if (
                uri.IsFile &&
                Path.IsPathFullyQualified(
                    uri.LocalPath))
            {
                return source;
            }
        }

        if (Path.IsPathFullyQualified(
                source))
        {
            return source;
        }

        throw new InvalidDataException(
            "Background update manifest source must be an HTTPS URL or absolute local file path.");
    }

    private static void ValidateProperties(
        JsonElement element,
        IReadOnlySet<string> allowedProperties,
        string context)
    {
        foreach (var property in
                 element.EnumerateObject())
        {
            if (!allowedProperties.Contains(
                    property.Name))
            {
                throw new InvalidDataException(
                    string.Concat(
                        "Unknown ",
                        context,
                        " property: ",
                        property.Name,
                        "."));
            }
        }
    }

    private static string GetRequiredString(
        JsonElement element,
        string propertyName)
    {
        if (
            !element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
                JsonValueKind.String)
        {
            throw new InvalidDataException(
                string.Concat(
                    "Background update property ",
                    propertyName,
                    " must be a string."));
        }

        return property.GetString()
            ?? throw new InvalidDataException(
                string.Concat(
                    "Background update property ",
                    propertyName,
                    " is missing."));
    }

    private static bool GetRequiredBoolean(
        JsonElement element,
        string propertyName)
    {
        if (
            !element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind is not
                JsonValueKind.True and not
                JsonValueKind.False)
        {
            throw new InvalidDataException(
                string.Concat(
                    "Background update property ",
                    propertyName,
                    " must be boolean."));
        }

        return property.GetBoolean();
    }

    private static int GetRequiredInt32(
        JsonElement element,
        string propertyName)
    {
        if (
            !element.TryGetProperty(
                propertyName,
                out var property))
        {
            throw new InvalidDataException(
                string.Concat(
                    "Background update property ",
                    propertyName,
                    " is required."));
        }

        return RequireInt32(
            property,
            propertyName);
    }

    private static int RequireInt32(
        JsonElement element,
        string propertyName)
    {
        if (
            element.ValueKind !=
                JsonValueKind.Number ||
            !element.TryGetInt32(
                out var value))
        {
            throw new InvalidDataException(
                string.Concat(
                    "Background update property ",
                    propertyName,
                    " must be an integer."));
        }

        return value;
    }
}
