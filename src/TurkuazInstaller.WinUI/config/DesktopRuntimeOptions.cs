// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopRuntimeOptions.cs
// 📌 Amac: WinUI runtime state, staging, lock, journal, log ve manifest trust store yollarini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.5.0
// Aciklama: Desktop state/staging/integration receipt, trust, UI profile ve reboot resume config degerlerini implementasyonlardan ayirir
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.WinUI.Config;

public sealed record DesktopRuntimeOptions(
    string StateRoot,
    string StagingRoot,
    string LockRoot,
    string JournalRoot,
    string LogRoot,
    string ResumeRoot,
    string IntegrationRoot,
    string ManifestTrustStorePath,
    string UiProfilePath,
    string BootstrapExecutablePath,
    string RebootResumeValueNamePrefix);
