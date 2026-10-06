// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/Gitea/GiteaReleaseProvider.cs
// 📌 Amac: Gitea Releases API uzerinden signed installer manifestini bulur ve release modeline donusturur
// 📌 Modul - Tool CSharp
// Version: 1.2.0
// Aciklama: Optional host-scoped Gitea token ile private API/manifest/.p7s isteklerini authorize eder; detached trust pipeline fail-closed kalir
//
// Bagimli Oldugu Katman: Tool | Service

using System.Text.Json;
using TurkuazInstaller.Contracts.Credentials;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Credentials;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Infrastructure.Providers.Gitea;

public sealed class GiteaReleaseProvider : IReleaseProvider
{
    private const string ListPathFormat =
        "repos/{0}/{1}/releases";

    private readonly HttpClient _httpClient;
    private readonly GiteaReleaseProviderOptions _options;
    private readonly RemoteManifestLoader _manifestLoader;
    private readonly IProviderCredentialResolver _credentialResolver;

    public GiteaReleaseProvider(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier,
        GiteaReleaseProviderOptions options)
        : this(
            httpClient,
            manifestReader,
            signatureVerifier,
            options,
            NullProviderCredentialResolver.Instance)
    {
    }

    public GiteaReleaseProvider(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier,
        GiteaReleaseProviderOptions options,
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

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                uri);

        authorization?.Apply(
            request);

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
                    cancellationToken,
                    authorization)
                .ConfigureAwait(false);

        return ProviderValidation.MatchRequest(
            parsed,
            packageId,
            channel);
    }

    private async Task<ProviderRequestAuthorization?>
        ResolveAuthorizationAsync(
            CancellationToken cancellationToken)
    {
        var accessToken =
            await _credentialResolver
                .ResolveAsync(
                    new ProviderCredentialRequest(
                        ProviderCredentialProvider.Gitea,
                        _options.ApiBaseUri.Authority),
                    cancellationToken)
                .ConfigureAwait(false);

        if (accessToken is null)
        {
            return null;
        }

        return new ProviderRequestAuthorization(
            accessToken,
            "token",
            _options.CredentialAuthorities);
    }
}
