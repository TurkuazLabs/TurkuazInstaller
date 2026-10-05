// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Artifacts/DefaultArtifactDownloader.cs
// 📌 Amac: HTTPS ve file artifactlarini staging alanina atomik olarak indiren Tool adapterini uygular
// 📌 Modul - Tool CSharp
// Version: 0.7.1
// Aciklama: Remote ve local artifact kopyasini .partial dosyasi uzerinden tamamlayip verified pipeline'a teslim eder
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
        var downloadRoot = Path.Combine(
            root,
            DownloadsDirectory,
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(downloadRoot);

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
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (artifact.Uri.Scheme == Uri.UriSchemeHttps)
            {
                await DownloadHttpsAsync(
                        artifact.Uri,
                        partialPath,
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
            throw;
        }
    }

    private async Task DownloadHttpsAsync(
        Uri uri,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var destination = CreateDestination(
            destinationPath);

        await source
            .CopyToAsync(
                destination,
                BufferSize,
                cancellationToken)
            .ConfigureAwait(false);

        await destination
            .FlushAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var destination = CreateDestination(
            destinationPath);

        await source
            .CopyToAsync(
                destination,
                BufferSize,
                cancellationToken)
            .ConfigureAwait(false);

        await destination
            .FlushAsync(cancellationToken)
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
}
