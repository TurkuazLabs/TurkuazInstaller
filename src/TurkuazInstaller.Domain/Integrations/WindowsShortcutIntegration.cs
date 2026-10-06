// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Integrations/WindowsShortcutIntegration.cs
// 📌 Amac: Signed manifest Windows shortcut aksiyonunu guvenli typed modelde tasir
// 📌 Modul - Domain CSharp
// Version: 1.0.2
// Aciklama: Action id, gorunen ad, lokasyon ve install root relative EXE yolunu strict dogrular
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Integrations;

public sealed record WindowsShortcutIntegration
{
    public WindowsShortcutIntegration(
        string id,
        string name,
        WindowsShortcutLocation location,
        string executableRelativePath)
    {
        Id = ValidateIdentifier(
            id,
            nameof(id));

        Name = ValidateName(
            name);

        Location = location;

        ExecutableRelativePath =
            ValidateExecutableRelativePath(
                executableRelativePath);
    }

    public string Id { get; }

    public string Name { get; }

    public WindowsShortcutLocation Location { get; }

    public string ExecutableRelativePath { get; }

    private static string ValidateIdentifier(
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (
            normalized.Length > 64 ||
            !char.IsLetterOrDigit(
                normalized[0]) ||
            normalized.Any(
                character =>
                    !char.IsLetterOrDigit(
                        character) &&
                    character is not
                        '.' and not
                        '_' and not
                        '-'))
        {
            throw new ArgumentException(
                "Windows integration id is invalid.",
                parameterName);
        }

        return normalized;
    }

    private static string ValidateName(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        var normalized =
            value.Trim();

        if (
            normalized.Length > 100 ||
            normalized is "." or ".." ||
            normalized.Any(
                character =>
                    character < 32 ||
                    character is
                        '<' or
                        '>' or
                        ':' or
                        '"' or
                        '/' or
                        '\\' or
                        '|' or
                        '?' or
                        '*'))
        {
            throw new ArgumentException(
                "Windows shortcut name is invalid.",
                nameof(value));
        }

        return normalized;
    }

    private static string ValidateExecutableRelativePath(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        var normalized =
            value.Trim()
                .Replace(
                    '\\',
                    '/');

        var segments =
            normalized.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        if (
            normalized.StartsWith(
                '/',
                StringComparison.Ordinal) ||
            normalized.Contains(
                ':') ||
            segments.Length == 0 ||
            !string.Equals(
                Path.GetExtension(
                    normalized),
                ".exe",
                StringComparison.OrdinalIgnoreCase) ||
            segments.Any(
                segment =>
                    segment is "." or ".."))
        {
            throw new ArgumentException(
                "Windows integration executable must be a relative .exe path without traversal.",
                nameof(value));
        }

        return normalized;
    }
}
