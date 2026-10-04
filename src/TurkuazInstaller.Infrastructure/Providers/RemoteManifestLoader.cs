// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/RemoteManifestLoader.cs
// 📌 Amac: HTTPS manifest metnini indirip ortak manifest parserina iletir
// 📌 Modul - Tool CSharp
// Version: 0.4.1
// Aciklama: Manifest URI'sini degistirmeden HTTPS guvenlik kontrolu uygular ve ortak parseri kullanir
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Infrastructure.Providers;

internal sealed class RemoteManifestLoader
{
    private readonly HttpClient _httpClient;
    private readonly InstallerManifestReader _manifestReader;

    public RemoteManifestLoader(HttpClient httpClient, InstallerManifestReader manifestReader)
    {
        _httpClient = httpClient;
        _manifestReader = manifestReader;
    }

    public async Task<PackageRelease> LoadAsync(Uri manifestUri, CancellationToken cancellationToken)
    {
        ProviderValidation.HttpsUri(manifestUri, nameof(manifestUri));
        var yaml = await _httpClient.GetStringAsync(manifestUri, cancellationToken).ConfigureAwait(false);
        return _manifestReader.Read(yaml);
    }
}
