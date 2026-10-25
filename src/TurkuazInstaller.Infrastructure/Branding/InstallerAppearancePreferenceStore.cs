// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/InstallerAppearancePreferenceStore.cs
// Amac: WinUI Sistem/Acik/Koyu gorunum secimini birbirinden bagimsiz ve kalici saklamak
// Modul - CSharp Config Repository
// Version: 0.1.0-qa
// Aciklama: Uretim trust politikasi ve tema sertifikalariyla ilgili degildir
// Bagimli Oldugu Katman: Infrastructure | Local UI Preference

using System.Text;
using System.Text.Json;

namespace TurkuazInstaller.Infrastructure.Branding;

public enum InstallerAppearanceMode
{
    System = 0,
    Light = 1,
    Dark = 2
}

/// <summary>
/// Per-user cosmetic preference only. This JSON can be changed by the user;
/// it is NEVER a source for signer pins, mandatory themes, product identities,
/// manifest URLs or any other security policy.
/// </summary>
public sealed class InstallerAppearancePreferenceStore
{
    private const int MaxPreferenceBytes = 4096;
    private readonly string _path;

    public InstallerAppearancePreferenceStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public InstallerAppearanceMode Read()
    {
        try
        {
            if (!File.Exists(_path) ||
                new FileInfo(_path).Length > MaxPreferenceBytes)
            {
                return InstallerAppearanceMode.System;
            }

            using var doc = JsonDocument.Parse(
                File.ReadAllBytes(_path),
                new JsonDocumentOptions
                {
                    MaxDepth = 2,
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow
                });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count() != 2 ||
                !root.TryGetProperty("schema_version", out var schema) ||
                schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out var version) || version != 1 ||
                !root.TryGetProperty("mode", out var mode) ||
                mode.ValueKind != JsonValueKind.String)
            {
                return InstallerAppearanceMode.System;
            }

            return mode.GetString() switch
            {
                "light" => InstallerAppearanceMode.Light,
                "dark" => InstallerAppearanceMode.Dark,
                "system" => InstallerAppearanceMode.System,
                _ => InstallerAppearanceMode.System
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or JsonException or ArgumentException)
        {
            // Corrupted or unavailable cosmetic state cannot block startup.
            return InstallerAppearanceMode.System;
        }
    }

    public void Save(InstallerAppearanceMode mode)
    {
        var value = mode switch
        {
            InstallerAppearanceMode.System => "system",
            InstallerAppearanceMode.Light => "light",
            InstallerAppearanceMode.Dark => "dark",
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(
            directory, ".appearance-" + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            var json = JsonSerializer.Serialize(new
            {
                schema_version = 1,
                mode = value
            });
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
