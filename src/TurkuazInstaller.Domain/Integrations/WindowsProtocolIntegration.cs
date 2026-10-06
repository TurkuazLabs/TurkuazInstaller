// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Integrations/WindowsProtocolIntegration.cs
// 📌 Amac: Signed manifest URL protocol registration aksiyonunu typed ve strict modelde tasir
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Protocol scheme ve install root relative EXE yolunu registry guvenlik siniri icin normalize eder
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Integrations;

public sealed record WindowsProtocolIntegration
{
    public WindowsProtocolIntegration(
        string scheme,
        string executableRelativePath)
    {
        Scheme =
            ValidateScheme(
                scheme);

        ExecutableRelativePath =
            new WindowsShortcutIntegration(
                "protocol-target",
                "Protocol Target",
                WindowsShortcutLocation.StartMenu,
                executableRelativePath)
                .ExecutableRelativePath;
    }

    public string Scheme { get; }

    public string ExecutableRelativePath { get; }

    private static string ValidateScheme(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        var normalized =
            value.Trim()
                .ToLowerInvariant();

        if (
            normalized.Length is < 2 or > 32 ||
            !char.IsLetter(
                normalized[0]) ||
            normalized.Any(
                character =>
                    !char.IsLetterOrDigit(
                        character) &&
                    character is not
                        '+' and not
                        '-' and not
                        '.'))
        {
            throw new ArgumentException(
                "Windows protocol scheme is invalid.",
                nameof(value));
        }

        return normalized;
    }
}
