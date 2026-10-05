// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/Gitea/GiteaReleaseProvider.cs
// 📌 Amac: Gitea Releases API uzerinden signed installer manifestini bulur ve release modeline donusturur
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Stable veya beta release assetinden manifest ve .p7s sidecarini ortak trust pipeline'inda fail-closed dogrular
//
// Bagimli Oldugu Katman: Tool | Service

using System.Text.Json;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Infrastructure.Providers.Gitea;

public sealed class GiteaReleaseProvider : IReleaseProvider
{
    private const string ListPathFormat =
        "repos/{0}/{1}/releases";

    private readonly HttpClient _httpClient;
    private readonly GiteaReleaseProviderOptions _options;
    private readonly RemoteManifestLoader _manifestLoader;

    public GiteaReleaseProvider(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier,
        GiteaReleaseProviderOptions options)
    {
        _httpClient =
            httpClient;

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
        var relativePath =
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                ListPathFormat,
                Uri.EscapeDataString(
                    _options.Owner),
                Uri.EscapeDataString(
                    _options.Repository));

        var uri =
            new Uri(
                _options.ApiBaseUri,
                relativePath);

        using var response =
            await _httpClient
                .GetAsync(
                    uri,
                    cancellationToken)
                .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var stream =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        var releases =
            await JsonSerializer
                .DeserializeAsync<List<RemoteReleaseDocument>>(
                    stream,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false)
            ?? new List<RemoteReleaseDocument>();

        var release =
            releases.FirstOrDefault(
                item =>
                    !item.Draft &&
                    item.Prerelease ==
                        (channel ==
                            ReleaseChannel.Beta));

        if (release is null)
        {
            return null;
        }

        var asset =
            release.Assets.FirstOrDefault(
                item =>
                    string.Equals(
                        item.Name,
                        _options.ManifestAssetName,
                        StringComparison.Ordinal));

        if (
            asset is null ||
            !Uri.TryCreate(
                asset.BrowserDownloadUrl,
                UriKind.Absolute,
                out var manifestUri))
        {
            return null;
        }

        ProviderValidation.HttpsUri(
            manifestUri,
            nameof(manifestUri));

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
