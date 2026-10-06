// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsVersionUpdatePolicyRepository.cs
// 📌 Amac: LocalAppData version-policy.json dosyasindan package/channel skip ve maximum-version pin policy resolve eder
// 📌 Modul - Repo CSharp
// Version: 1.0.0
// Aciklama: Missing config policy yok kabul edilir; strict JSON, duplicate identity ve invalid SemVer fail-closed reddedilir
//
// Bagimli Oldugu Katman: Repo | Config | Service

using System.Text.Json;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsVersionUpdatePolicyRepository
    : IVersionUpdatePolicyRepository
{
    private const int SchemaVersion = 1;
    private const int MaximumEntryCount = 100;
    private const int MaximumSkippedVersionCount = 100;

    private static readonly IReadOnlySet<string>
        AllowedRootProperties =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "schema_version",
                "entries"
            };

    private static readonly IReadOnlySet<string>
        AllowedEntryProperties =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "package_id",
                "channel",
                "maximum_version",
                "skipped_versions"
            };

    private readonly string _configPath;

    public WindowsVersionUpdatePolicyRepository(
        string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            configPath);

        _configPath =
            Path.GetFullPath(
                configPath);
    }

    public Task<VersionUpdatePolicy?> GetAsync(
        PackageId packageId,
        ReleaseChannel channel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);

        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(
                _configPath))
        {
            return Task.FromResult<VersionUpdatePolicy?>(
                null);
        }

        var policies =
            ReadPolicies();

        return Task.FromResult(
            policies.FirstOrDefault(
                policy =>
                    policy.PackageId == packageId &&
                    policy.Channel == channel));
    }

    private IReadOnlyList<VersionUpdatePolicy> ReadPolicies()
    {
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
                "Version policy root must be a JSON object.");
        }

        ValidateProperties(
            root,
            AllowedRootProperties,
            "version policy");

        var schemaVersion =
            GetRequiredInt32(
                root,
                "schema_version");

        if (schemaVersion != SchemaVersion)
        {
            throw new InvalidDataException(
                "Version policy schema_version is unsupported.");
        }

        if (
            !root.TryGetProperty(
                "entries",
                out var entriesElement))
        {
            return Array.Empty<VersionUpdatePolicy>();
        }

        if (
            entriesElement.ValueKind !=
                JsonValueKind.Array ||
            entriesElement.GetArrayLength() >
                MaximumEntryCount)
        {
            throw new InvalidDataException(
                "Version policy entries must be an array with at most 100 items.");
        }

        var policies =
            new List<VersionUpdatePolicy>();

        var identities =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var entry in
                 entriesElement.EnumerateArray())
        {
            if (
                entry.ValueKind !=
                JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "Version policy entry must be a JSON object.");
            }

            ValidateProperties(
                entry,
                AllowedEntryProperties,
                "version policy entry");

            var packageId =
                PackageId.Parse(
                    GetRequiredString(
                        entry,
                        "package_id"));

            var channel =
                ParseChannel(
                    GetRequiredString(
                        entry,
                        "channel"));

            var identity =
                string.Concat(
                    packageId.Value,
                    "|",
                    channel.ToString());

            if (!identities.Add(
                    identity))
            {
                throw new InvalidDataException(
                    "Version policy contains duplicate package/channel entries.");
            }

            var maximumVersion =
                ReadOptionalVersion(
                    entry,
                    "maximum_version");

            var skippedVersions =
                ReadSkippedVersions(
                    entry);

            if (
                maximumVersion is null &&
                skippedVersions.Count == 0)
            {
                throw new InvalidDataException(
                    "Version policy entry must define maximum_version or skipped_versions.");
            }

            policies.Add(
                new VersionUpdatePolicy(
                    packageId,
                    channel,
                    maximumVersion,
                    skippedVersions));
        }

        return policies.AsReadOnly();
    }

    private static IReadOnlyList<SemanticVersion>
        ReadSkippedVersions(
            JsonElement entry)
    {
        if (
            !entry.TryGetProperty(
                "skipped_versions",
                out var skippedElement))
        {
            return Array.Empty<SemanticVersion>();
        }

        if (
            skippedElement.ValueKind !=
                JsonValueKind.Array ||
            skippedElement.GetArrayLength() >
                MaximumSkippedVersionCount)
        {
            throw new InvalidDataException(
                "skipped_versions must be an array with at most 100 versions.");
        }

        var versions =
            new List<SemanticVersion>();

        var unique =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (var item in
                 skippedElement.EnumerateArray())
        {
            if (item.ValueKind !=
                JsonValueKind.String)
            {
                throw new InvalidDataException(
                    "skipped_versions entries must be strings.");
            }

            var value =
                item.GetString();

            if (string.IsNullOrWhiteSpace(
                    value))
            {
                throw new InvalidDataException(
                    "skipped_versions entry must not be empty.");
            }

            var version =
                SemanticVersion.Parse(
                    value.Trim());

            var normalized =
                version.ToString();

            if (!unique.Add(
                    normalized))
            {
                throw new InvalidDataException(
                    "skipped_versions contains duplicate versions.");
            }

            versions.Add(
                version);
        }

        return versions.AsReadOnly();
    }

    private static SemanticVersion? ReadOptionalVersion(
        JsonElement entry,
        string propertyName)
    {
        if (
            !entry.TryGetProperty(
                propertyName,
                out var property))
        {
            return null;
        }

        if (property.ValueKind !=
            JsonValueKind.String)
        {
            throw new InvalidDataException(
                string.Concat(
                    propertyName,
                    " must be a semantic version string."));
        }

        var value =
            property.GetString();

        if (string.IsNullOrWhiteSpace(
                value))
        {
            throw new InvalidDataException(
                string.Concat(
                    propertyName,
                    " must not be empty."));
        }

        return SemanticVersion.Parse(
            value.Trim());
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
                        "Version policy channel must be stable or beta.")
            };
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
                    propertyName,
                    " must be a string."));
        }

        return property.GetString()
            ?? throw new InvalidDataException(
                string.Concat(
                    propertyName,
                    " is missing."));
    }

    private static int GetRequiredInt32(
        JsonElement element,
        string propertyName)
    {
        if (
            !element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
                JsonValueKind.Number ||
            !property.TryGetInt32(
                out var value))
        {
            throw new InvalidDataException(
                string.Concat(
                    propertyName,
                    " must be an integer."));
        }

        return value;
    }
}
