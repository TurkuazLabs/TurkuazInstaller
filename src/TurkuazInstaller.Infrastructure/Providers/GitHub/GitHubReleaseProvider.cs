// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/GitHub/GitHubReleaseProvider.cs
// 📌 Amac: GitHub Releases uzerinden signed Community installer manifestini bulur ve release modeline donusturur
// 📌 Modul - Tool CSharp
// Version: 1.2.0
// Aciklama: Optional host-scoped Bearer credential ile private release API/manifest/.p7s isteklerini authorize eder; detached trust pipeline fail-closed kalir
//
// Bagimli Oldugu Katman: Tool | Service

using System.Net.Http.Headers;
using System.Text.Json;
using TurkuazInstaller.Contracts.Credentials;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Credentials;
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
    private readonly IProviderCredentialResolver _credentialResolver;

    public GitHubReleaseProvider(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier,
        GitHubReleaseProviderOptions options)
        : this(
            httpClient,
            manifestReader,
            signatureVerifier,
            options,
            NullProviderCredentialResolver.Instance)
    {
    }

    public GitHubReleaseProvider(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier,
        GitHubReleaseProviderOptions options,
        IProviderCredentialResolver credentialResolver)
    {
        ArgumentNullException.ThrowIfNull(
            credentialResolver);

        _httpClient =
            httpClient;

        _options =
            options;

        _credentialResolver =
            credentialResolver;

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
        var authorization =
            await ResolveAuthorizationAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        var release =
            channel == ReleaseChannel.Stable
                ? await GetStableReleaseAsync(
                        authorization,
                        cancellationToken)
                    .ConfigureAwait(false)
                : await GetBetaReleaseAsync(
                        authorization,
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
                    cancellationToken,
                    authorization)
                .ConfigureAwait(false);

        return ProviderValidation.MatchRequest(
            parsed,
            packageId,
            channel);
    }

    private async Task<RemoteReleaseDocument?>
        GetStableReleaseAsync(
            ProviderRequestAuthorization? authorization,
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
                uri,
                authorization);

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
            ProviderRequestAuthorization? authorization,
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
        Uri uri,
        ProviderRequestAuthorization? authorization)
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

        authorization?.Apply(
            request);

        return request;
    }

    private async Task<ProviderRequestAuthorization?>
        ResolveAuthorizationAsync(
            CancellationToken cancellationToken)
    {
        var accessToken =
            await _credentialResolver
                .ResolveAsync(
                    new ProviderCredentialRequest(
                        ProviderCredentialProvider.GitHub,
                        _options.ApiBaseUri.Authority),
                    cancellationToken)
                .ConfigureAwait(false);

        if (accessToken is null)
        {
            return null;
        }

        return new ProviderRequestAuthorization(
            accessToken,
            "Bearer",
            _options.CredentialAuthorities);
    }

    private Uri BuildApiUri(
        string relativePath)
    {
        return new Uri(
            _options.ApiBaseUri,
            relativePath);
    }
}
