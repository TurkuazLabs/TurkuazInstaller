// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/RemoteManifestLoader.cs
// 📌 Amac: HTTPS manifest ve detached signature byte'larini indirip verification sonrasi ortak parsera iletir
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Remote manifesti parserdan once CMS detached signature pipeline'inda package trust policy ile fail-closed dogrular
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
        CancellationToken cancellationToken)
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
                    cancellationToken)
                .ConfigureAwait(false);

        var detachedSignature =
            await GetRequiredBytesAsync(
                    signatureUri,
                    SignatureMissingMessage,
                    cancellationToken)
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
        CancellationToken cancellationToken)
    {
        using var response =
            await _httpClient
                .GetAsync(
                    uri,
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

        return await response.Content
            .ReadAsByteArrayAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }
}
