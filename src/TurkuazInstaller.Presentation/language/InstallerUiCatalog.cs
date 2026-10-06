// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/language/InstallerUiCatalog.cs
// 📌 Amac: Varsayilan Language metinleri ile typed UI profile override degerlerini tek resolverda birlestirir
// 📌 Modul - Language CSharp
// Version: 1.0.0
// Aciklama: Branding ve desteklenen label override alanlarini ViewModel icin fallback-guvenli sunar
//
// Bagimli Oldugu Katman: Language | View | Config

using TurkuazInstaller.Contracts.Branding;

namespace TurkuazInstaller.Presentation.Language;

public sealed class InstallerUiCatalog
{
    private readonly InstallerUiProfile _profile;

    public InstallerUiCatalog(
        InstallerUiProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile);

        _profile = profile;
    }

    public string? Culture =>
        _profile.Culture;

    public string WindowTitle =>
        ResolveBranding(
            _profile.Branding.WindowTitle,
            InstallerUiLabels.WindowTitle);

    public string HeaderTitle =>
        ResolveBranding(
            _profile.Branding.HeaderTitle,
            InstallerUiLabels.HeaderTitle);

    public string HeaderSubtitle =>
        ResolveBranding(
            _profile.Branding.HeaderSubtitle,
            InstallerUiLabels.HeaderSubtitle);

    public string Footer =>
        ResolveBranding(
            _profile.Branding.Footer,
            InstallerUiLabels.Footer);

    public string Resolve(
        InstallerUiLabelKey key,
        string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            fallback);

        return _profile.Labels.TryGetValue(
            key,
            out var value)
            ? value
            : fallback;
    }

    private static string ResolveBranding(
        string? value,
        string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value;
    }
}
