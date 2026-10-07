// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Packages/Velopack/VelopackConventions.cs
// 📌 Amac: Velopack executable, directory, extension ve CLI arguman sabitlerini merkezi Tool config olarak tanimlar
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Apply, delta patch, repair, rollback ve uninstall process komutlarinda magic string kullanilmasini engeller
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Infrastructure.Packages.Velopack;

internal static class VelopackConventions
{
    public const string SetupExtension = ".exe";
    public const string FullPackageExtension = ".nupkg";
    public const string SetupFallbackName = "Setup.exe";
    public const string FullPackageFallbackName = "package-full.nupkg";
    public const string DeltaPackageFallbackName = "package-delta.nupkg";
    public const string FullPackageSuffix = "-full.nupkg";
    public const string DeltaPackageSuffix = "-delta.nupkg";
    public const string PartialSuffix = ".partial";
    public const string UpdateExecutableName = "Update.exe";
    public const string PackagesDirectoryName = "packages";
    public const string CurrentDirectoryName = "current";

    public const string SilentArgument = "--silent";
    public const string InstallToArgument = "--installto";
    public const string RootDirectoryArgument = "--rootDir";
    public const string PackageDirectoryArgument = "--packageDir";
    public const string ApplyCommand = "apply";
    public const string PatchCommand = "patch";
    public const string UninstallCommand = "uninstall";
    public const string NoRestartArgument = "--norestart";
    public const string PackageArgument = "--package";
    public const string OldArgument = "--old";
    public const string DeltaArgument = "--delta";
    public const string OutputArgument = "--output";
}
