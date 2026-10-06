// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/config/CliRuntimeOptions.cs
// 📌 Amac: CLI state, staging, diagnostics, resume, trust ve executable yollarini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Silent runtime storage ve reboot resume ayarlarini Service/Repo/Tool implementasyonlarindan ayirir
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.Cli.Config;

internal sealed record CliRuntimeOptions(
    string StateRoot,
    string StagingRoot,
    string LockRoot,
    string JournalRoot,
    string LogRoot,
    string ResumeRoot,
    string ManifestTrustStorePath,
    string ExecutablePath,
    string RebootResumeValueNamePrefix);
