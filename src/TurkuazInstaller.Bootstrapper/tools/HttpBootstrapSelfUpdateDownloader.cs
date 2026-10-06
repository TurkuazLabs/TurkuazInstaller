// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/tools/HttpBootstrapSelfUpdateDownloader.cs
// 📌 Amac: Kesfedilen bootstrap replacement executable'ini staging alanina indirir ve exact size/SHA-256 dogrular
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Oversize, undersize veya digest mismatch artifacti handoff katmanina vermeden reddeder
//
// Bagimli Oldugu Katman: Tool | Service

using System.Security.Cryptography;
using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Bootstrapper.Tools;

internal sealed class HttpBootstrapSelfUpdateDownloader
    : IBootstrapSelfUpdateDownloader
{
    private const int BufferSize = 81920;

    private readonly HttpClient _httpClient;

    public HttpBootstrapSelfUpdateDownloader(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> DownloadAsync(
        BootstrapSelfUpdateRelease release,
        string stagingRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingRoot);

        if (
            !string.Equals(
                Path.GetFileName(
                    release.AssetName),
                release.AssetName,
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(
                    release.AssetName),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Bootstrap self-update asset name is invalid.");
        }

        var operationRoot =
            Path.Combine(
                Path.GetFullPath(
                    stagingRoot),
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            operationRoot);

        var finalPath =
            Path.Combine(
                operationRoot,
                release.AssetName);

        var temporaryPath =
            string.Concat(
                finalPath,
                ".tmp");

        try
        {
            using var response =
                await _httpClient
                    .GetAsync(
                        release.ArtifactUri,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            if (
                response.Content.Headers.ContentLength is long contentLength &&
                contentLength != release.SizeBytes)
            {
                throw new InvalidDataException(
                    "Bootstrap self-update Content-Length does not match release metadata.");
            }

            await using (
                var source =
                    await response.Content
                        .ReadAsStreamAsync(
                            cancellationToken)
                        .ConfigureAwait(false))
            await using (
                var destination =
                    new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        BufferSize,
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan))
            {
                var buffer =
                    new byte[
                        BufferSize];

                long total = 0;

                while (true)
                {
                    var read =
                        await source
                            .ReadAsync(
                                buffer,
                                cancellationToken)
                            .ConfigureAwait(false);

                    if (read == 0)
                    {
                        break;
                    }

                    total += read;

                    if (total > release.SizeBytes)
                    {
                        throw new InvalidDataException(
                            "Bootstrap self-update artifact exceeded expected size.");
                    }

                    await destination
                        .WriteAsync(
                            buffer.AsMemory(
                                0,
                                read),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await destination
                    .FlushAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                if (total != release.SizeBytes)
                {
                    throw new InvalidDataException(
                        "Bootstrap self-update artifact size does not match release metadata.");
                }
            }

            string actualSha256;

            await using (
                var stream =
                    new FileStream(
                        temporaryPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        BufferSize,
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan))
            {
                var digest =
                    await SHA256
                        .HashDataAsync(
                            stream,
                            cancellationToken)
                        .ConfigureAwait(false);

                actualSha256 =
                    Convert
                        .ToHexString(
                            digest)
                        .ToLowerInvariant();
            }

            if (
                !string.Equals(
                    actualSha256,
                    release.ArtifactDigest.Sha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Bootstrap self-update SHA-256 digest does not match release metadata.");
            }

            File.Move(
                temporaryPath,
                finalPath);

            return finalPath;
        }
        catch
        {
            TryDelete(
                temporaryPath);

            TryDelete(
                finalPath);

            throw;
        }
    }

    private static void TryDelete(
        string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(
                    path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
