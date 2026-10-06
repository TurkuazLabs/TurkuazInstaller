// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsNetworkProxyOptionsReader.cs
// 📌 Amac: LocalAppData network.json dosyasindan proxy politikasini strict ve NativeAOT-safe sekilde okur
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Config yoksa system proxy dondurur; malformed/unknown alan veya gecersiz custom proxy ayarini fail-closed reddeder
//
// Bagimli Oldugu Katman: Tool | Config

using System.Text.Json;
using TurkuazInstaller.Contracts.Networking;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsNetworkProxyOptionsReader
{
    private const int SupportedSchemaVersion = 1;

    private static readonly IReadOnlySet<string>
        AllowedRootProperties =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "schema_version",
                "mode",
                "custom_proxy",
                "bypass_local",
                "use_default_credentials"
            };

    private readonly string _configPath;

    public WindowsNetworkProxyOptionsReader(
        string configPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            configPath);

        _configPath =
            Path.GetFullPath(
                configPath);
    }

    public NetworkProxyOptions Read()
    {
        if (!File.Exists(_configPath))
        {
            return NetworkProxyOptions.SystemDefault();
        }

        using var stream =
            new FileStream(
                _configPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        using var document =
            JsonDocument.Parse(
                stream);

        var root =
            document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                "Network proxy config root must be an object.");
        }

        ValidateProperties(
            root);

        if (
            !root.TryGetProperty(
                "schema_version",
                out var schemaVersion) ||
            !schemaVersion.TryGetInt32(
                out var parsedSchemaVersion) ||
            parsedSchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                "Network proxy config schema_version is unsupported.");
        }

        var modeText =
            ReadRequiredString(
                root,
                "mode");

        var mode =
            modeText switch
            {
                "system" =>
                    NetworkProxyMode.System,
                "direct" =>
                    NetworkProxyMode.Direct,
                "custom" =>
                    NetworkProxyMode.Custom,
                _ =>
                    throw new InvalidDataException(
                        "Network proxy mode is unsupported.")
            };

        var bypassLocal =
            ReadOptionalBoolean(
                root,
                "bypass_local",
                defaultValue: true);

        var useDefaultCredentials =
            ReadOptionalBoolean(
                root,
                "use_default_credentials",
                defaultValue: false);

        Uri? customProxyUri = null;

        if (
            root.TryGetProperty(
                "custom_proxy",
                out var customProxy))
        {
            if (
                customProxy.ValueKind !=
                    JsonValueKind.String ||
                !Uri.TryCreate(
                    customProxy.GetString(),
                    UriKind.Absolute,
                    out customProxyUri))
            {
                throw new InvalidDataException(
                    "Network custom_proxy must be an absolute URI string.");
            }
        }

        try
        {
            return new NetworkProxyOptions(
                mode,
                customProxyUri,
                bypassLocal,
                useDefaultCredentials);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "Network proxy config is invalid.",
                exception);
        }
    }

    private static void ValidateProperties(
        JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (
                !AllowedRootProperties.Contains(
                    property.Name))
            {
                throw new InvalidDataException(
                    string.Concat(
                        "Unknown network proxy config property: ",
                        property.Name));
            }
        }
    }

    private static string ReadRequiredString(
        JsonElement root,
        string propertyName)
    {
        if (
            !root.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
                JsonValueKind.String ||
            string.IsNullOrWhiteSpace(
                property.GetString()))
        {
            throw new InvalidDataException(
                string.Concat(
                    "Network proxy config property is required: ",
                    propertyName));
        }

        return property.GetString()!;
    }

    private static bool ReadOptionalBoolean(
        JsonElement root,
        string propertyName,
        bool defaultValue)
    {
        if (
            !root.TryGetProperty(
                propertyName,
                out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True =>
                true,
            JsonValueKind.False =>
                false,
            _ =>
                throw new InvalidDataException(
                    string.Concat(
                        "Network proxy config property must be boolean: ",
                        propertyName))
        };
    }
}
