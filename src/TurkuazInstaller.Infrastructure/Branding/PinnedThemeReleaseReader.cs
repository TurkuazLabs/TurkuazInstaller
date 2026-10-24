// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/PinnedThemeReleaseReader.cs
// Amac: Dogru imzali fakat eski/yanlis urun temalarinin kabulunu engellemek
// Modul - CSharp Security Adapter
// Version: 0.1.0-qa
// Aciklama: Beklenen tema kimligi ve exact version uygulamanin kendi derlemesinden gelir
// Bagimli Oldugu Katman: Infrastructure | Signed Pack | Product Release

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Branding;

public sealed record PinnedThemeRelease(string ThemeId, string ExactVersion)
{
    public static PinnedThemeRelease Create(string themeId, string exactVersion)
    {
        if (string.IsNullOrWhiteSpace(themeId) || themeId.Length > 48 ||
            themeId[0] is < 'a' or > 'z' ||
            themeId.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')) ||
            SemanticVersion.Parse(exactVersion).ToString() != exactVersion)
        {
            throw new ArgumentException("Invalid compiled theme identity or canonical version.");
        }

        return new PinnedThemeRelease(themeId, exactVersion);
    }
}

/// <summary>
/// Prevents downgrade/substitution of still-valid signed archives by requiring
/// the reviewed theme ID and EXACT release version in the signed application.
/// No user-writable sidecar is permitted to alter this requirement.
/// </summary>
public sealed class PinnedThemeReleaseReader : IVerifiedThemePackReader
{
    private readonly IVerifiedThemePackReader _inner;
    private readonly PinnedThemeRelease _pin;

    public PinnedThemeReleaseReader(
        IVerifiedThemePackReader inner, PinnedThemeRelease pin)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _pin = pin ?? throw new ArgumentNullException(nameof(pin));
    }

    public async Task<VerifiedThemePack> ReadAsync(
        Stream source, PackageId expectedPackageId,
        CancellationToken cancellationToken = default)
    {
        var pack = await _inner.ReadAsync(
            source, expectedPackageId, cancellationToken).ConfigureAwait(false);

        if (!string.Equals(pack.ThemeId, _pin.ThemeId, StringComparison.Ordinal) ||
            !string.Equals(pack.Version, _pin.ExactVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Signed theme identity or version does not match the compiled release policy.");
        }

        return pack;
    }
}
