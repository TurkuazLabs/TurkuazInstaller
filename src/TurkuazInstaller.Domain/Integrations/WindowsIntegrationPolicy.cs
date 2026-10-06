// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Integrations/WindowsIntegrationPolicy.cs
// 📌 Amac: Package Windows shortcut ve URL protocol aksiyonlarini tek typed install policy altinda toplar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Duplicate shortcut id/protocol scheme degerlerini reddeder ve backward-compatible Empty policy saglar
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Integrations;

public sealed record WindowsIntegrationPolicy
{
    private const int MaximumActionsPerType = 32;

    public WindowsIntegrationPolicy(
        IReadOnlyList<WindowsShortcutIntegration> shortcuts,
        IReadOnlyList<WindowsProtocolIntegration> protocols)
    {
        ArgumentNullException.ThrowIfNull(
            shortcuts);
        ArgumentNullException.ThrowIfNull(
            protocols);

        if (
            shortcuts.Count > MaximumActionsPerType ||
            protocols.Count > MaximumActionsPerType)
        {
            throw new ArgumentException(
                "Windows integration policy contains too many actions.");
        }

        EnsureUnique(
            shortcuts.Select(
                item =>
                    item.Id),
            "Windows shortcut ids must be unique.");

        EnsureUnique(
            protocols.Select(
                item =>
                    item.Scheme),
            "Windows protocol schemes must be unique.");

        Shortcuts =
            shortcuts.ToArray();

        Protocols =
            protocols.ToArray();
    }

    public IReadOnlyList<WindowsShortcutIntegration> Shortcuts
    {
        get;
    }

    public IReadOnlyList<WindowsProtocolIntegration> Protocols
    {
        get;
    }

    public bool HasActions =>
        Shortcuts.Count > 0 ||
        Protocols.Count > 0;

    public static WindowsIntegrationPolicy Empty
    {
        get;
    } = new(
        Array.Empty<WindowsShortcutIntegration>(),
        Array.Empty<WindowsProtocolIntegration>());

    private static void EnsureUnique(
        IEnumerable<string> values,
        string message)
    {
        var set =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            if (!set.Add(
                    value))
            {
                throw new ArgumentException(
                    message);
            }
        }
    }
}
