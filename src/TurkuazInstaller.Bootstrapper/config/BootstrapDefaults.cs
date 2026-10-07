// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/config/BootstrapDefaults.cs
// 📌 Amac: Native bootstrap runtime requirement, app layout ve self-update retry varsayilanlarini tek config katmaninda tanimlar
// 📌 Modul - Config CSharp
// Version: 1.2.0
// Aciklama: Windows baseline, x64/ARM64 policy, app yolu, handoff retry ve architecture-safe GitHub bootstrap self-update discovery varsayilanlarini merkezilestirir
//
// Bagimli Oldugu Katman: Config

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Contracts.Bootstrap;
using TurkuazInstaller.Platform.Windows.Tools;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Bootstrapper.Config;

internal static class BootstrapDefaults
{
    private const int Windows10Build1809 = 17763;
    private const int ReplacementAttempts = 12;
    private const int ReplacementRetryMilliseconds = 250;
    private const int DiscoveryTimeoutSeconds = 5;
    private const int DownloadTimeoutSeconds = 120;

    private const string ProductDirectoryName = "TurkuazInstaller";
    private const string SelfUpdateDirectoryName = "bootstrap-update";
    private const string X64BootstrapExecutableName =
        "TurkuazInstaller.Bootstrapper.exe";
    private const string Arm64BootstrapExecutableName =
        "TurkuazInstaller.Bootstrapper-win-arm64.exe";
    private const string LatestReleaseApiUrl =
        "https://api.github.com/repos/TurkuazLabs/TurkuazInstaller/releases/latest";

    private const string AppDirectoryName = "app";
    private const string DesktopExecutableName =
        "TurkuazInstaller.WinUI.exe";

    public static BootstrapRequirements CreateRequirements()
    {
        return new BootstrapRequirements(
            new Version(
                10,
                0,
                Windows10Build1809,
                0),
            new[]
            {
                BootstrapCpuArchitecture.X64,
                BootstrapCpuArchitecture.Arm64
            });
    }

    public static SelfUpdateHandoffOptions CreateSelfUpdateOptions()
    {
        return new SelfUpdateHandoffOptions(
            ReplacementAttempts,
            TimeSpan.FromMilliseconds(
                ReplacementRetryMilliseconds));
    }

    public static BootstrapRuntimeOptions CreateRuntimeOptions()
    {
        return new BootstrapRuntimeOptions(
            Path.Combine(
                AppDirectoryName,
                DesktopExecutableName));
    }

    public static BootstrapSelfUpdateOptions
        CreateSelfUpdateDiscoveryOptions(
            BootstrapCpuArchitecture processArchitecture)
    {
        var assemblyVersion =
            typeof(BootstrapDefaults)
                .Assembly
                .GetName()
                .Version
            ?? throw new InvalidOperationException(
                "Bootstrap assembly version could not be resolved.");

        var currentVersion =
            SemanticVersion.Parse(
                string.Concat(
                    assemblyVersion.Major,
                    ".",
                    assemblyVersion.Minor,
                    ".",
                    Math.Max(
                        0,
                        assemblyVersion.Build)));

        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return new BootstrapSelfUpdateOptions(
            currentVersion,
            new Uri(
                LatestReleaseApiUrl,
                UriKind.Absolute),
            ResolveBootstrapAssetName(
                processArchitecture),
            Path.Combine(
                localAppData,
                ProductDirectoryName,
                SelfUpdateDirectoryName),
            TimeSpan.FromSeconds(
                DiscoveryTimeoutSeconds),
            TimeSpan.FromSeconds(
                DownloadTimeoutSeconds));
    }

    private static string ResolveBootstrapAssetName(
        BootstrapCpuArchitecture processArchitecture)
    {
        return processArchitecture switch
        {
            BootstrapCpuArchitecture.X64 =>
                X64BootstrapExecutableName,
            BootstrapCpuArchitecture.Arm64 =>
                Arm64BootstrapExecutableName,
            _ =>
                throw new PlatformNotSupportedException(
                    "Bootstrap self-update architecture is unsupported.")
        };
    }
}
