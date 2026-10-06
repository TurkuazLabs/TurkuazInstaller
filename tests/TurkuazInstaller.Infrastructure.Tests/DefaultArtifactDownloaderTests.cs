// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/DefaultArtifactDownloaderTests.cs
// 📌 Amac: Artifact downloaderin signed size_bytes sinirini HTTPS ve local file transferinde uyguladigini dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Exact-size basari, remote Content-Length mismatch ve local file size mismatch senaryolarini kapsar
//
// Bagimli Oldugu Katman: Tool | Domain

using System.Text;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Infrastructure.Artifacts;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class DefaultArtifactDownloaderTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static readonly Uri ArtifactUri =
        new(
            "https://downloads.example.invalid/example.bin");

    [Fact]
    public async Task DownloadAsync_HttpsExactSize_Succeeds()
    {
        const string payload = "abc";

        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    new Dictionary<string, string>
                    {
                        [ArtifactUri.AbsoluteUri] =
                            payload
                    }));

        var root =
            CreateTemporaryDirectory();

        try
        {
            var downloader =
                new DefaultArtifactDownloader(
                    client);

            var path =
                await downloader.DownloadAsync(
                    CreateArtifact(
                        ArtifactUri,
                        Encoding.UTF8.GetByteCount(
                            payload)),
                    root,
                    CancellationToken.None);

            Assert.True(
                File.Exists(
                    path));

            Assert.Equal(
                payload,
                await File.ReadAllTextAsync(
                    path));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_HttpsSizeMismatch_RejectsTransfer()
    {
        const string payload = "oversized";

        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    new Dictionary<string, string>
                    {
                        [ArtifactUri.AbsoluteUri] =
                            payload
                    }));

        var root =
            CreateTemporaryDirectory();

        try
        {
            var downloader =
                new DefaultArtifactDownloader(
                    client);

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    downloader.DownloadAsync(
                        CreateArtifact(
                            ArtifactUri,
                            3),
                        root,
                        CancellationToken.None));

            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*",
                    SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAsync_LocalSizeMismatch_RejectsTransfer()
    {
        var source =
            Path.GetTempFileName();

        var root =
            CreateTemporaryDirectory();

        try
        {
            await File.WriteAllTextAsync(
                source,
                "local-payload");

            var downloader =
                new DefaultArtifactDownloader(
                    new HttpClient());

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    downloader.DownloadAsync(
                        CreateArtifact(
                            new Uri(
                                source),
                            3),
                        root,
                        CancellationToken.None));

            Assert.Empty(
                Directory.EnumerateFiles(
                    root,
                    "*",
                    SearchOption.AllDirectories));
        }
        finally
        {
            File.Delete(
                source);

            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private static ArtifactDescriptor CreateArtifact(
        Uri uri,
        long sizeBytes)
    {
        return new ArtifactDescriptor(
            uri,
            ArtifactDigest.ParseSha256(
                Digest),
            sizeBytes);
    }

    private static string CreateTemporaryDirectory()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                string.Concat(
                    "turkuaz-installer-download-",
                    Guid.NewGuid().ToString("N")));

        Directory.CreateDirectory(
            path);

        return path;
    }
}
