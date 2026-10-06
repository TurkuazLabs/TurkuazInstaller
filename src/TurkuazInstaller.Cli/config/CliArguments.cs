// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/config/CliArguments.cs
// 📌 Amac: CLI komut ve option adlarini magic string kullanmadan merkezilestirir
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Public CLI ve internal reboot-resume command-line protokol sabitlerini tanimlar
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.Cli.Config;

internal static class CliArguments
{
    public const string InstallCommand = "install";
    public const string UpdateCommand = "update";
    public const string RepairCommand = "repair";
    public const string RollbackCommand = "rollback";
    public const string UninstallCommand = "uninstall";

    public const string PackageOption = "--package";
    public const string ManifestOption = "--manifest";
    public const string RollbackManifestOption = "--rollback-manifest";
    public const string ChannelOption = "--channel";
    public const string TargetOption = "--target";
    public const string SilentOption = "--silent";
    public const string InternalResumePackageOption = "--resume-package";

    public const string StableChannel = "stable";
    public const string BetaChannel = "beta";
}
