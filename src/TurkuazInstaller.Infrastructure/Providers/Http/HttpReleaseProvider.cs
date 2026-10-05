// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/Http/HttpReleaseProvider.cs
// 📌 Amac: Generic HTTPS manifest endpointlerinden verified typed release bilgisi saglar
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Stable ve beta manifest adreslerini config uzerinden secer ve detached signature verification zorunlulugunu ortak loadera uygular
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Infrastructure.Providers.Http;

public sealed class HttpReleaseProvider : IReleaseProvider
{
    private readonly HttpReleaseProviderOptions _options;
    private readonly RemoteManifestLoader _manifestLoader;

    public HttpReleaseProvider(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier,
        HttpReleaseProviderOptions options)
    {
        _options =
            options;

        _manifestLoader =
            new RemoteManifestLoader(
                httpClient,
                manifestReader,
                signatureVerifier);
    }

    public async Task<PackageRelease?> GetLatestReleaseAsync(
        PackageId packageId,
        ReleaseChannel channel,
        CancellationToken cancellationToken)
    {
        var manifestUri =
            channel == ReleaseChannel.Stable
                ? _options.StableManifestUri
                : _options.BetaManifestUri;

        var parsed =
            await _manifestLoader
                .LoadAsync(
                    manifestUri,
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        return ProviderValidation.MatchRequest(
            parsed,
            packageId,
            channel);
    }
}
