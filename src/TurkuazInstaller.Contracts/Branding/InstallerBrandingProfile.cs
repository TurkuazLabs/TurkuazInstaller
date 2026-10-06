// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Branding/InstallerBrandingProfile.cs
// 📌 Amac: Installer pencere markalama alanlarini typed contract olarak tasir
// 📌 Modul - Contract CSharp
// Version: 1.0.0
// Aciklama: Window title, header title/subtitle ve footer override degerlerini tutar
//
// Bagimli Oldugu Katman: Service | Repo | View

namespace TurkuazInstaller.Contracts.Branding;

public sealed record InstallerBrandingProfile(
    string? WindowTitle,
    string? HeaderTitle,
    string? HeaderSubtitle,
    string? Footer)
{
    public static InstallerBrandingProfile Empty { get; } =
        new(
            null,
            null,
            null,
            null);
}
