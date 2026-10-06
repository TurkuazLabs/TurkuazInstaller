// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Artifacts/DefaultArtifactDownloader.cs
// 📌 Amac: HTTPS ve file artifactlarini staging alanina atomik olarak indiren Tool adapterini uygular
// 📌 Modul - Tool CSharp
// Version: 0.8.2
// Aciklama: Bounded/exact transfer uygular ve basarisiz operation staging kalintilarini best-effort temizler
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Domain.Artifacts;

namespace TurkuazInstaller.Infrastructure.Artifacts;

public sealed class DefaultArtifactDownloader : IArtifactDownloader
{
    private const string DownloadsDirectory = "downloads";
    private const string PartialSuffix = ".partial";
    private const string FallbackFileName = "artifact.bin";
    private const int BufferSize = 81920;

    private readonly HttpClient _httpClient;

    public DefaultArtifactDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> DownloadAsync(
        ArtifactDescriptor artifact,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);

        var root = Path.GetFullPath(stagingDirectory);
        var downloadsRoot =
            Path.Combine(
                root,
                DownloadsDirectory);

        var downloadRoot =
            Path.Combine(
                downloadsRoot,
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            downloadRoot);

        var fileName = ResolveFileName(artifact.Uri);
        var destinationPath = Path.Combine(
            downloadRoot,
            fileName);

        var partialPath = string.Concat(
            destinationPath,
            PartialSuffix);

        try
        {
            if (artifact.Uri.IsFile)
            {
                await CopyFileAsync(
                        artifact.Uri.LocalPath,
                        partialPath,
                        artifact.SizeBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (artifact.Uri.Scheme == Uri.UriSchemeHttps)
            {
                await DownloadHttpsAsync(
                        artifact.Uri,
                        partialPath,
                        artifact.SizeBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException(
                    "Artifact downloader only supports HTTPS and file URIs.");
            }

            File.Move(
                partialPath,
                destinationPath);

            return destinationPath;
        }
        catch
        {
            TryDelete(partialPath);
            TryDeleteDirectory(downloadRoot);
            TryDeleteDirectory(downloadsRoot);
            throw;
        }
    }

    private async Task DownloadHttpsAsync(
        Uri uri,
        string destinationPath,
        long expectedSizeBytes,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        if (
            response.Content.Headers.ContentLength is long contentLength &&
            contentLength != expectedSizeBytes)
        {
            throw new InvalidDataException(
                "Artifact Content-Length does not match signed release metadata.");
        }

        await using var source =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await CopyExactAsync(
                source,
                destinationPath,
                expectedSizeBytes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        long expectedSizeBytes,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        if (source.Length != expectedSizeBytes)
        {
            throw new InvalidDataException(
                "Local artifact size does not match signed release metadata.");
        }

        await CopyExactAsync(
                source,
                destinationPath,
                expectedSizeBytes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CopyExactAsync(
        Stream source,
        string destinationPath,
        long expectedSizeBytes,
        CancellationToken cancellationToken)
    {
        await using var destination =
            CreateDestination(
                destinationPath);

        var buffer =
            new byte[
                BufferSize];

        long totalBytes = 0;

        while (true)
        {
            var read =
                await source
                    .ReadAsync(
                        buffer.AsMemory(
                            0,
                            buffer.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            totalBytes += read;

            if (totalBytes > expectedSizeBytes)
            {
                throw new InvalidDataException(
                    "Artifact exceeded the size declared by signed release metadata.");
            }

            await destination
                .WriteAsync(
                    buffer.AsMemory(
                        0,
                        read),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (totalBytes != expectedSizeBytes)
        {
            throw new InvalidDataException(
                "Artifact size does not match signed release metadata.");
        }

        await destination
            .FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static FileStream CreateDestination(
        string destinationPath)
    {
        return new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    private static string ResolveFileName(Uri uri)
    {
        var sourcePath = uri.IsFile
            ? uri.LocalPath
            : uri.AbsolutePath;

        var fileName = Path.GetFileName(
            Uri.UnescapeDataString(sourcePath));

        return string.IsNullOrWhiteSpace(fileName)
            ? FallbackFileName
            : fileName;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            if (
                Directory.Exists(
                    path) &&
                !Directory.EnumerateFileSystemEntries(
                    path)
                    .Any())
            {
                Directory.Delete(
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
