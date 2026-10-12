// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Branding/InstallerUiProfile.cs
// 📌 Amac: Branding ve localization override verisini tek typed UI profile contractinda birlestirir
// 📌 Modul - Contract CSharp
// Version: 1.1.0
// Aciklama: Culture, branding ve desteklenen label override degerlerini immutable contract olarak tasir
//
// Bagimli Oldugu Katman: Service | Repo | View | Language

namespace TurkuazInstaller.Contracts.Branding;

public sealed record InstallerUiProfile(
    string? Culture,
    InstallerBrandingProfile Branding,
    IReadOnlyDictionary<InstallerUiLabelKey, string> Labels,
    InstallerPromotionProfile? Promotion = null)
{
    public static InstallerUiProfile Empty { get; } =
        new(
            null,
            InstallerBrandingProfile.Empty,
            new Dictionary<InstallerUiLabelKey, string>(),
            InstallerPromotionProfile.Default);
}
