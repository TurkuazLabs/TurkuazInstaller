// 📄 Dosya Yolu: /tests/TurkuazInstaller.Bootstrapper.Tests/GitHubBootstrapSelfUpdateDiscoveryTests.cs
// 📌 Amac: GitHub bootstrap self-update discovery response parsing ve version kararlarini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Newer stable asset, current version skip ve invalid digest fail-closed davranislarini kapsar
//
// Bagimli Oldugu Katman: Tool | Service | Config

using System.Net;
using System.Text;
using TurkuazInstaller.Bootstrapper.Config;
using TurkuazInstaller.Bootstrapper.Tools;
using TurkuazInstaller.Domain.Releases;
using Xunit;

namespace TurkuazInstaller.Bootstrapper.Tests;

public sealed class GitHubBootstrapSelfUpdateDiscoveryTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task GetLatestAsync_NewerStableRelease_ReturnsPinnedAsset()
    {
        using var client =
            CreateClient(
                CreateReleaseJson(
                    "v1.1.0",
                    string.Concat(
                        "sha256:",
                        Digest)));

        var discovery =
            new GitHubBootstrapSelfUpdateDiscovery(
                client,
                CreateOptions());

        var release =
            await discovery.GetLatestAsync(
                SemanticVersion.Parse(
                    "1.0.0"),
                CancellationToken.None);

        Assert.NotNull(
            release);

        Assert.Equal(
            "1.1.0",
            release!.Version.ToString());

        Assert.Equal(
            "TurkuazInstaller.Bootstrapper.exe",
            release.AssetName);

        Assert.Equal(
            Digest,
            release.ArtifactDigest.Sha256);

        Assert.Equal(
            1024,
            release.SizeBytes);
    }

    [Fact]
    public async Task GetLatestAsync_CurrentRelease_ReturnsNull()
    {
        using var client =
            CreateClient(
                CreateReleaseJson(
                    "v1.1.0",
                    string.Concat(
                        "sha256:",
                        Digest)));

        var discovery =
            new GitHubBootstrapSelfUpdateDiscovery(
                client,
                CreateOptions());

        var release =
            await discovery.GetLatestAsync(
                SemanticVersion.Parse(
                    "1.1.0"),
                CancellationToken.None);

        Assert.Null(
            release);
    }

    [Fact]
    public async Task GetLatestAsync_NewerReleaseWithoutSha256Digest_Throws()
    {
        using var client =
            CreateClient(
                CreateReleaseJson(
                    "v1.1.0",
                    "sha512:deadbeef"));

        var discovery =
            new GitHubBootstrapSelfUpdateDiscovery(
                client,
                CreateOptions());

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                discovery.GetLatestAsync(
                    SemanticVersion.Parse(
                        "1.0.0"),
                    CancellationToken.None));
    }

    private static BootstrapSelfUpdateOptions CreateOptions()
    {
        return new BootstrapSelfUpdateOptions(
            SemanticVersion.Parse(
                "1.0.0"),
            new Uri(
                "https://api.github.com/repos/TurkuazLabs/TurkuazInstaller/releases/latest"),
            "TurkuazInstaller.Bootstrapper.exe",
            Path.GetTempPath(),
            TimeSpan.FromSeconds(
                5),
            TimeSpan.FromMinutes(
                2));
    }

    private static HttpClient CreateClient(
        string responseBody)
    {
        return new HttpClient(
            new StubHttpMessageHandler(
                responseBody));
    }

    private static string CreateReleaseJson(
        string tagName,
        string digest)
    {
        return string.Concat(
            "{",
            "\"tag_name\":\"",
            tagName,
            "\",",
            "\"draft\":false,",
            "\"prerelease\":false,",
            "\"assets\":[{",
            "\"name\":\"TurkuazInstaller.Bootstrapper.exe\",",
            "\"state\":\"uploaded\",",
            "\"browser_download_url\":\"https://github.com/TurkuazLabs/TurkuazInstaller/releases/download/v1.1.0/TurkuazInstaller.Bootstrapper.exe\",",
            "\"size\":1024,",
            "\"digest\":\"",
            digest,
            "\"",
            "}]}");
    }

    private sealed class StubHttpMessageHandler
        : HttpMessageHandler
    {
        private readonly string _responseBody;

        public StubHttpMessageHandler(
            string responseBody)
        {
            _responseBody = responseBody;
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
                        new StringContent(
                            _responseBody,
                            Encoding.UTF8,
                            "application/json")
                });
        }
    }
}
