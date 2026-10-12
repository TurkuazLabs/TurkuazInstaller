// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Branding/InstallerPromotionProfile.cs
// 📌 Amac: Tum urunlerin installer ilerleme ekraninda kullanacagi yerel tanitim kartini tanimlar
// 📌 Modul - Contract CSharp
// Version: 1.0.0
// Aciklama: Internet/tracking gerektirmeyen ve kapatilabilen first-party tanitim contracti
//
// Bagimli Oldugu Katman: Service | Repo | View | Config

namespace TurkuazInstaller.Contracts.Branding;

public sealed record InstallerPromotionProfile(
    bool? Enabled,
    string? Eyebrow,
    string? Title,
    string? Description,
    string? Provider)
{
    public static InstallerPromotionProfile Empty { get; } =
        new(null, null, null, null, null);
}
