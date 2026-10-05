// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopRuntimeOptions.cs
// 📌 Amac: WinUI runtime state, staging, lock, journal, log ve manifest trust store yollarini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.2.0
// Aciklama: Desktop runtime storage ve manifest trust anchor dosya yolunu Service/Repo/Tool implementasyonlarindan ayirir
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.WinUI.Config;

public sealed record DesktopRuntimeOptions(
    string StateRoot,
    string StagingRoot,
    string LockRoot,
    string JournalRoot,
    string LogRoot,
    string ManifestTrustStorePath);
