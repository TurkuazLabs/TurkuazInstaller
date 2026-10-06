// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/tools/GitHubBootstrapSelfUpdateDiscovery.cs
// 📌 Amac: GitHub latest release API uzerinden daha yeni bootstrap self-update assetini kesfeder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Stable tag, exact asset, HTTPS URL, size ve GitHub sha256 digest alanlarini fail-closed parse eder
//
// Bagimli Oldugu Katman: Tool | Service | Config

using System.Net;
using System.Text.Json;
using TurkuazInstaller.Bootstrapper.Config;
using TurkuazInstaller.Contracts.Bootstrap;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Bootstrapper.Tools;

internal sealed class GitHubBootstrapSelfUpdateDiscovery
    : IBootstrapSelfUpdateDiscovery
{
    private const string AcceptHeader =
        "application/vnd.github+json";

    private const string ApiVersionHeaderName =
        "X-GitHub-Api-Version";

    private const string ApiVersion =
        "2026-03-10";

    private const string UserAgent =
        "TurkuazInstaller-Bootstrapper";

    private const string Sha256Prefix =
        "sha256:";

    private readonly HttpClient _httpClient;
    private readonly BootstrapSelfUpdateOptions _options;

    public GitHubBootstrapSelfUpdateDiscovery(
        HttpClient httpClient,
        BootstrapSelfUpdateOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<BootstrapSelfUpdateRelease?> GetLatestAsync(
        SemanticVersion currentVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                _options.LatestReleaseUri);

        request.Headers.Accept.ParseAdd(
            AcceptHeader);

        request.Headers.TryAddWithoutValidation(
            ApiVersionHeaderName,
            ApiVersion);

        request.Headers.UserAgent.ParseAdd(
            UserAgent);

        HttpResponseMessage response;

        try
        {
            response =
                await _httpClient
                    .SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        using (response)
        {
            if (
                response.StatusCode ==
                HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream =
                await response.Content
                    .ReadAsStreamAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            using var document =
                await JsonDocument
                    .ParseAsync(
                        stream,
                        cancellationToken:
                            cancellationToken)
                    .ConfigureAwait(false);

            return ParseRelease(
                document.RootElement,
                currentVersion);
        }
    }

    private BootstrapSelfUpdateRelease? ParseRelease(
        JsonElement root,
        SemanticVersion currentVersion)
    {
        if (
            root.TryGetProperty(
                "draft",
                out var draft) &&
            draft.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        if (
            root.TryGetProperty(
                "prerelease",
                out var prerelease) &&
            prerelease.ValueKind == JsonValueKind.True)
        {
            return null;
        }

        var tagName =
            RequireString(
                root,
                "tag_name");

        var versionText =
            tagName.StartsWith(
                "v",
                StringComparison.OrdinalIgnoreCase)
                ? tagName[1..]
                : tagName;

        var latestVersion =
            SemanticVersion.Parse(
                versionText);

        if (
            latestVersion.PreRelease is not null ||
            latestVersion <= currentVersion)
        {
            return null;
        }

        if (
            !root.TryGetProperty(
                "assets",
                out var assets) ||
            assets.ValueKind !=
                JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "GitHub release assets are missing.");
        }

        JsonElement? selected = null;

        foreach (var asset in assets.EnumerateArray())
        {
            if (
                !string.Equals(
                    TryGetString(
                        asset,
                        "name"),
                    _options.AssetName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (selected is not null)
            {
                throw new InvalidDataException(
                    "GitHub release contains duplicate bootstrap self-update assets.");
            }

            selected = asset;
        }

        var releaseAsset =
            selected
            ?? throw new InvalidDataException(
                "Latest release does not contain the required bootstrap self-update asset.");

        if (
            !string.Equals(
                TryGetString(
                    releaseAsset,
                    "state"),
                "uploaded",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Bootstrap self-update release asset is not uploaded.");
        }

        var downloadUri =
            new Uri(
                RequireString(
                    releaseAsset,
                    "browser_download_url"),
                UriKind.Absolute);

        if (
            !string.Equals(
                downloadUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                downloadUri.Host,
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Bootstrap self-update release asset URL is not an allowed GitHub HTTPS URL.");
        }

        if (
            !releaseAsset.TryGetProperty(
                "size",
                out var sizeElement) ||
            !sizeElement.TryGetInt64(
                out var sizeBytes) ||
            sizeBytes <= 0)
        {
            throw new InvalidDataException(
                "Bootstrap self-update release asset size is invalid.");
        }

        var digest =
            RequireString(
                releaseAsset,
                "digest");

        if (
            !digest.StartsWith(
                Sha256Prefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Bootstrap self-update release asset SHA-256 digest is missing.");
        }

        return new BootstrapSelfUpdateRelease(
            latestVersion,
            _options.AssetName,
            downloadUri,
            ArtifactDigest.ParseSha256(
                digest[Sha256Prefix.Length..]),
            sizeBytes);
    }

    private static string RequireString(
        JsonElement element,
        string propertyName)
    {
        var value =
            TryGetString(
                element,
                propertyName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                string.Concat(
                    "GitHub release field is missing: ",
                    propertyName));
        }

        return value;
    }

    private static string? TryGetString(
        JsonElement element,
        string propertyName)
    {
        if (
            !element.TryGetProperty(
                propertyName,
                out var property) ||
            property.ValueKind !=
                JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}
