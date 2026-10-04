// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Packages/Velopack/VelopackConventions.cs
// 📌 Amac: Velopack executable, klasor, extension ve CLI argument sabitlerini merkezilestirir
// 📌 Modul - Tool CSharp
// Version: 0.5.0
// Aciklama: Setup.exe ve Update.exe entegrasyonunda magic string kullanilmasini engeller
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Infrastructure.Packages.Velopack;

internal static class VelopackConventions
{
    public const string SetupExtension = ".exe";
    public const string FullPackageExtension = ".nupkg";
    public const string UpdateExecutableName = "Update.exe";
    public const string PackagesDirectoryName = "packages";
    public const string CurrentDirectoryName = "current";
    public const string PartialSuffix = ".partial";
    public const string SetupFallbackName = "package-Setup.exe";
    public const string FullPackageFallbackName = "package-full.nupkg";

    public const string SilentArgument = "--silent";
    public const string InstallToArgument = "--installto";
    public const string RootDirectoryArgument = "--rootDir";
    public const string PackageDirectoryArgument = "--packageDir";
    public const string ApplyCommand = "apply";
    public const string NoRestartArgument = "--norestart";
    public const string PackageArgument = "--package";
}
