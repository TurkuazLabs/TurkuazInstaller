// 📄 Dosya Yolu: /tests/TurkuazInstaller.Bootstrapper.Tests/HttpBootstrapSelfUpdateDownloaderTests.cs
// 📌 Amac: Bootstrap self-update downloader size ve SHA-256 integrity sinirlarini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Exact artifact download ve digest mismatch fail-closed davranisini gercek temp dosya uzerinde dogrular
//
// Bagimli Oldugu Katman: Tool | Service

using System.Net;
using System.Security.Cryptography;
using TurkuazInstaller.Bootstrapper.Tools;
using TurkuazInstaller.Contracts.Bootstrap;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Releases;
using Xunit;

namespace TurkuazInstaller.Bootstrapper.Tests;

public sealed class HttpBootstrapSelfUpdateDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_ExactSizeAndDigest_WritesReplacement()
    {
        var payload =
            new byte[]
            {
                1,
                2,
                3,
                4,
                5
            };

        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    payload));

        var root =
            CreateTemporaryRoot();

        try
        {
            var path =
                await new HttpBootstrapSelfUpdateDownloader(
                        client)
                    .DownloadAsync(
                        CreateRelease(
                            payload),
                        root,
                        CancellationToken.None);

            Assert.True(
                File.Exists(
                    path));

            Assert.Equal(
                payload,
                await File.ReadAllBytesAsync(
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
    public async Task DownloadAsync_DigestMismatch_ThrowsBeforeReturningReplacement()
    {
        var payload =
            new byte[]
            {
                9,
                8,
                7
            };

        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    payload));

        var root =
            CreateTemporaryRoot();

        try
        {
            var release =
                new BootstrapSelfUpdateRelease(
                    SemanticVersion.Parse(
                        "1.1.0"),
                    "TurkuazInstaller.Bootstrapper.exe",
                    new Uri(
                        "https://github.com/TurkuazLabs/TurkuazInstaller/releases/download/v1.1.0/TurkuazInstaller.Bootstrapper.exe"),
                    ArtifactDigest.ParseSha256(
                        new string(
                            '0',
                            64)),
                    payload.Length);

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    new HttpBootstrapSelfUpdateDownloader(
                            client)
                        .DownloadAsync(
                            release,
                            root,
                            CancellationToken.None));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private static BootstrapSelfUpdateRelease CreateRelease(
        byte[] payload)
    {
        var digest =
            SHA256.HashData(
                payload);

        return new BootstrapSelfUpdateRelease(
            SemanticVersion.Parse(
                "1.1.0"),
            "TurkuazInstaller.Bootstrapper.exe",
            new Uri(
                "https://github.com/TurkuazLabs/TurkuazInstaller/releases/download/v1.1.0/TurkuazInstaller.Bootstrapper.exe"),
            ArtifactDigest.ParseSha256(
                Convert
                    .ToHexString(
                        digest)
                    .ToLowerInvariant()),
            payload.Length);
    }

    private static string CreateTemporaryRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        return root;
    }

    private sealed class StubHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly byte[] _payload;

        public StubHttpMessageHandler(
            byte[] payload)
        {
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content =
                        new ByteArrayContent(
                            _payload)
                });
        }
    }
}
