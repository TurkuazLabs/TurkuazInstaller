// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Updates/BackgroundUpdatePolicy.cs
// 📌 Amac: Session background update policy enablement, interval ve check entry listesini typed olarak tasir
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: Missing config icin disabled default destekler; policy yalniz read-only discovery semantigi tasir
//
// Bagimli Oldugu Katman: Service | Config

namespace TurkuazInstaller.Contracts.Updates;

public sealed record BackgroundUpdatePolicy(
    bool Enabled,
    TimeSpan Interval,
    IReadOnlyList<BackgroundUpdatePolicyEntry> Entries)
{
    public static BackgroundUpdatePolicy Disabled()
    {
        return new BackgroundUpdatePolicy(
            false,
            TimeSpan.FromMinutes(
                60),
            Array.Empty<BackgroundUpdatePolicyEntry>());
    }
}
