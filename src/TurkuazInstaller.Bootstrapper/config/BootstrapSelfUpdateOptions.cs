// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/config/BootstrapSelfUpdateOptions.cs
// 📌 Amac: Bootstrap self-update discovery kaynagi, current version, asset adi ve staging root ayarlarini typed configte toplar
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Service ve GitHub discovery Tool icindeki update magic degerlerini merkezi config katmanina tasir
//
// Bagimli Oldugu Katman: Config | Service | Tool

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Bootstrapper.Config;

internal sealed record BootstrapSelfUpdateOptions
{
    public BootstrapSelfUpdateOptions(
        SemanticVersion currentVersion,
        Uri latestReleaseUri,
        string assetName,
        string stagingRoot)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        ArgumentNullException.ThrowIfNull(latestReleaseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);

        if (
            !latestReleaseUri.IsAbsoluteUri ||
            !string.Equals(
                latestReleaseUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Bootstrap self-update discovery URI must use HTTPS.",
                nameof(latestReleaseUri));
        }

        if (
            !string.Equals(
                Path.GetFileName(assetName),
                assetName,
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(assetName),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Bootstrap self-update asset name must be a direct executable file name.",
                nameof(assetName));
        }

        CurrentVersion = currentVersion;
        LatestReleaseUri = latestReleaseUri;
        AssetName = assetName.Trim();
        StagingRoot = Path.GetFullPath(stagingRoot);
    }

    public SemanticVersion CurrentVersion { get; }

    public Uri LatestReleaseUri { get; }

    public string AssetName { get; }

    public string StagingRoot { get; }
}
