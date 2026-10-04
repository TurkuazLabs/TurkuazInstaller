// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/File/FileReleaseProvider.cs
// 📌 Amac: Air-gapped ve local test senaryolari icin dosyadan installer manifesti okur
// 📌 Modul - Tool CSharp
// Version: 0.4.0
// Aciklama: Stable ve beta local manifestlerini ortak parser uzerinden typed release modeline donusturur
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Infrastructure.Providers.File;

public sealed class FileReleaseProvider : IReleaseProvider
{
    private readonly FileReleaseProviderOptions _options;
    private readonly InstallerManifestReader _manifestReader;

    public FileReleaseProvider(InstallerManifestReader manifestReader, FileReleaseProviderOptions options)
    {
        _manifestReader = manifestReader;
        _options = options;
    }

    public async Task<PackageRelease?> GetLatestReleaseAsync(
        PackageId packageId,
        ReleaseChannel channel,
        CancellationToken cancellationToken)
    {
        var path = channel == ReleaseChannel.Stable
            ? _options.StableManifestPath
            : _options.BetaManifestPath;

        if (!System.IO.File.Exists(path))
        {
            return null;
        }

        var yaml = await System.IO.File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var parsed = _manifestReader.Read(yaml);
        return ProviderValidation.MatchRequest(parsed, packageId, channel);
    }
}
