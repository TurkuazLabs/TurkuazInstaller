// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/RemoteManifestLoader.cs
// 📌 Amac: HTTPS manifest ve detached signature byte'larini indirip verification sonrasi ortak parsera iletir
// 📌 Modul - Tool CSharp
// Version: 1.3.0
// Aciklama: Remote manifest ve detached signature isteklerini host-scoped authorization ve bounded streaming ile parser/verification oncesi fail-closed sinirlar
//
// Bagimli Oldugu Katman: Tool | Service

using System.Net;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Infrastructure.Providers;

internal sealed class RemoteManifestLoader
{
    private const string ManifestMissingMessage =
        "Installer manifest could not be downloaded.";

    private const string SignatureMissingMessage =
        "Detached installer manifest signature is required.";

    private readonly HttpClient _httpClient;
    private readonly VerifiedManifestReader
        _verifiedManifestReader;

    public RemoteManifestLoader(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier)
    {
        ArgumentNullException.ThrowIfNull(
            httpClient);
        ArgumentNullException.ThrowIfNull(
            manifestReader);
        ArgumentNullException.ThrowIfNull(
            signatureVerifier);

        _httpClient =
            httpClient;

        _verifiedManifestReader =
            new VerifiedManifestReader(
                manifestReader,
                signatureVerifier);
    }

    public async Task<PackageRelease> LoadAsync(
        Uri manifestUri,
        PackageId packageId,
        CancellationToken cancellationToken,
        ProviderRequestAuthorization? authorization = null)
    {
        ProviderValidation.HttpsUri(
            manifestUri,
            nameof(manifestUri));

        var signatureUri =
            ManifestSignatureConventions
                .GetDetachedSignatureUri(
                    manifestUri);

        ProviderValidation.HttpsUri(
            signatureUri,
            nameof(signatureUri));

        var manifestContent =
            await GetRequiredBytesAsync(
                    manifestUri,
                    ManifestMissingMessage,
                    ManifestContentLimits.MaximumManifestBytes,
                    cancellationToken,
                    authorization)
                .ConfigureAwait(false);

        var detachedSignature =
            await GetRequiredBytesAsync(
                    signatureUri,
                    SignatureMissingMessage,
                    ManifestContentLimits.MaximumDetachedSignatureBytes,
                    cancellationToken,
                    authorization)
                .ConfigureAwait(false);

        return await _verifiedManifestReader
            .ReadAsync(
                manifestContent,
                detachedSignature,
                packageId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<byte[]> GetRequiredBytesAsync(
        Uri uri,
        string notFoundMessage,
        int maximumBytes,
        CancellationToken cancellationToken,
        ProviderRequestAuthorization? authorization)
    {
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
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);

        if (
            response.StatusCode ==
            HttpStatusCode.NotFound)
        {
            throw new InvalidDataException(
                notFoundMessage);
        }

        response.EnsureSuccessStatusCode();

        if (
            response.Content.Headers.ContentLength is long contentLength &&
            contentLength > maximumBytes)
        {
            throw new InvalidDataException(
                "Remote installer metadata exceeds the configured maximum size.");
        }

        await using var stream =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await BoundedContentReader
            .ReadAsync(
                stream,
                maximumBytes,
                "Remote installer metadata",
                cancellationToken)
            .ConfigureAwait(false);
    }
}
