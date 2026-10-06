// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/config/CliPathDefaults.cs
// 📌 Amac: CLI runtime icin ortak TurkuazInstaller local storage ve trust yollarini merkezi uretir
// 📌 Modul - Config CSharp
// Version: 1.1.0
// Aciklama: WinUI ile ayni state/journal/resume/integration kokunu kullanir ve CLI reboot resume executable yolunu sabitler
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.Cli.Config;

internal static class CliPathDefaults
{
    private const string ProductDirectory = "TurkuazInstaller";
    private const string StateDirectory = "state";
    private const string StagingDirectory = "staging";
    private const string LockDirectory = "locks";
    private const string JournalDirectory = "journal";
    private const string LogDirectory = "logs";
    private const string ResumeDirectory = "resume";
    private const string IntegrationDirectory = "integrations";
    private const string ConfigDirectory = "config";
    private const string ManifestTrustFileName = "manifest-trust.yml";
    private const string RebootResumeValueNamePrefix = "TurkuazInstaller.Cli.Resume";

    public static CliRuntimeOptions CreateRuntimeOptions()
    {
        var executablePath =
            Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "CLI executable path could not be resolved.");

        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var productRoot =
            Path.Combine(
                localAppData,
                ProductDirectory);

        return new CliRuntimeOptions(
            Path.Combine(productRoot, StateDirectory),
            Path.Combine(productRoot, StagingDirectory),
            Path.Combine(productRoot, LockDirectory),
            Path.Combine(productRoot, JournalDirectory),
            Path.Combine(productRoot, LogDirectory),
            Path.Combine(productRoot, ResumeDirectory),
            Path.Combine(productRoot, IntegrationDirectory),
            Path.Combine(
                productRoot,
                ConfigDirectory,
                ManifestTrustFileName),
            Path.GetFullPath(executablePath),
            RebootResumeValueNamePrefix);
    }
}
