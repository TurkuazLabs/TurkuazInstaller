// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/config/BootstrapRuntimeOptions.cs
// 📌 Amac: Bootstrap tarafindan baslatilacak desktop uygulamasinin dagitim-relative yolunu typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Combined distribution layout bilgisini Service kodundaki magic path degerlerinden ayirir
//
// Bagimli Oldugu Katman: Config | Service

namespace TurkuazInstaller.Bootstrapper.Config;

internal sealed record BootstrapRuntimeOptions
{
    public BootstrapRuntimeOptions(
        string applicationRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            applicationRelativePath);

        if (Path.IsPathRooted(applicationRelativePath))
        {
            throw new ArgumentException(
                "Bootstrap application path must be relative.",
                nameof(applicationRelativePath));
        }

        var normalized =
            applicationRelativePath
                .Replace(
                    Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar)
                .Trim();

        if (
            normalized
                .Split(
                    Path.DirectorySeparatorChar,
                    StringSplitOptions.RemoveEmptyEntries)
                .Any(
                    part =>
                        string.Equals(
                            part,
                            "..",
                            StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Bootstrap application path cannot escape the distribution root.",
                nameof(applicationRelativePath));
        }

        if (
            !string.Equals(
                Path.GetExtension(normalized),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Bootstrap application path must reference an executable.",
                nameof(applicationRelativePath));
        }

        ApplicationRelativePath = normalized;
    }

    public string ApplicationRelativePath { get; }
}
