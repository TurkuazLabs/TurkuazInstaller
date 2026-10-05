// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/GitHub/GitHubReleaseProvider.cs
// 📌 Amac: GitHub Releases uzerinden signed Community installer manifestini bulur ve release modeline donusturur
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Stable latest veya beta prerelease seciminden sonra manifest ve .p7s sidecarini ortak trust pipeline'inda dogrular
//
// Bagimli Oldugu Katman: Tool | Service

using System.Net.Http.Headers;
using System.Text.Json;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Infrastructure.Providers.GitHub;

public sealed class GitHubReleaseProvider : IReleaseProvider
{
    private const string LatestPathFormat =
        "repos/{0}/{1}/releases/latest";

    private const string ListPathFormat =
        "repos/{0}/{1}/releases?per_page=30";

    private readonly HttpClient _httpClient;
    private readonly GitHubReleaseProviderOptions _options;
    private readonly RemoteManifestLoader _manifestLoader;

    public GitHubReleaseProvider(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier,
        GitHubReleaseProviderOptions options)
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
        var release =
            channel == ReleaseChannel.Stable
                ? await GetStableReleaseAsync(
                        cancellationToken)
                    .ConfigureAwait(false)
                : await GetBetaReleaseAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

        if (release is null)
        {
            return null;
        }

        var manifestUri =
            FindManifestUri(
                release);

        if (manifestUri is null)
        {
            return null;
        }

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

    private async Task<RemoteReleaseDocument?>
        GetStableReleaseAsync(
            CancellationToken cancellationToken)
    {
        var uri =
            BuildApiUri(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    LatestPathFormat,
                    Uri.EscapeDataString(
                        _options.Owner),
                    Uri.EscapeDataString(
                        _options.Repository)));

        using var request =
            CreateRequest(
                uri);

        using var response =
            await _httpClient
                .SendAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            response.StatusCode ==
            System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var stream =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await JsonSerializer
            .DeserializeAsync<RemoteReleaseDocument>(
                stream,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<RemoteReleaseDocument?>
        GetBetaReleaseAsync(
            CancellationToken cancellationToken)
    {
        var uri =
            BuildApiUri(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    ListPathFormat,
                    Uri.EscapeDataString(
                        _options.Owner),
                    Uri.EscapeDataString(
                        _options.Repository)));

        using var request =
            CreateRequest(
                uri);

        using var response =
            await _httpClient
                .SendAsync(
                    request,
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

        return releases.FirstOrDefault(
            item =>
                !item.Draft &&
                item.Prerelease);
    }

    private Uri? FindManifestUri(
        RemoteReleaseDocument release)
    {
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
                out var uri))
        {
            return null;
        }

        ProviderValidation.HttpsUri(
            uri,
            nameof(uri));

        return uri;
    }

    private HttpRequestMessage CreateRequest(
        Uri uri)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                uri);

        request.Headers.UserAgent.Add(
            ProductInfoHeaderValue.Parse(
                _options.UserAgent));

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));

        return request;
    }

    private Uri BuildApiUri(
        string relativePath)
    {
        return new Uri(
            _options.ApiBaseUri,
            relativePath);
    }
}
