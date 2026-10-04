// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/GiteaReleaseProviderTests.cs
// 📌 Amac: Gitea providerin beta release assetinden manifest bulma davranisini dogrular
// 📌 Modul - Test CSharp
// Version: 0.4.0
// Aciklama: Gitea API ve manifest response'larini stub ederek provider contractini test eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.Gitea;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class GiteaReleaseProviderTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_LoadsBetaManifestAsset()
    {
        const string apiUrl = "https://gitea.example.test/api/v1/repos/turkuaz/example/releases";
        const string manifestUrl = "https://gitea.example.test/downloads/installer-manifest.yml";
        var releaseJson = $$"""
[{"draft":false,"prerelease":true,"assets":[{"name":"{{ProviderTestData.ManifestAssetName}}","browser_download_url":"{{manifestUrl}}"}]}]
""";

        using var client = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, string>
        {
            [apiUrl] = releaseJson,
            [manifestUrl] = ProviderTestData.Manifest(ReleaseChannel.Beta, "5.0.0-beta.1")
        }));

        var provider = new GiteaReleaseProvider(
            client,
            new InstallerManifestReader(),
            new GiteaReleaseProviderOptions(
                new Uri("https://gitea.example.test/api/v1/"),
                "turkuaz",
                "example",
                ProviderTestData.ManifestAssetName));

        var release = await provider.GetLatestReleaseAsync(
            PackageId.Parse(ProviderTestData.PackageId),
            ReleaseChannel.Beta,
            CancellationToken.None);

        Assert.Equal("5.0.0-beta.1", release?.Version.ToString());
    }
}
