// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/GitHubReleaseProviderTests.cs
// 📌 Amac: GitHub providerin stable release assetinden signed manifest bulma davranisini dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: GitHub API, manifest ve .p7s response'larini stub ederek provider trust contractini test eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.GitHub;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class GitHubReleaseProviderTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_LoadsVerifiedStableManifestAsset()
    {
        const string apiUrl =
            "https://api.github.test/repos/turkuaz/example/releases/latest";

        const string manifestUrl =
            "https://downloads.github.test/installer-manifest.yml";

        var signatureUrl =
            string.Concat(
                manifestUrl,
                ManifestSignatureConventions.DetachedSignatureSuffix);

        var releaseJson =
            $$"""
{"draft":false,"prerelease":false,"assets":[{"name":"{{ProviderTestData.ManifestAssetName}}","browser_download_url":"{{manifestUrl}}"}]}
""";

        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    new Dictionary<string, string>
                    {
                        [apiUrl] =
                            releaseJson,
                        [manifestUrl] =
                            ProviderTestData.Manifest(
                                ReleaseChannel.Stable,
                                "4.0.0"),
                        [signatureUrl] =
                            ProviderTestData.DetachedSignature
                    }));

        var signatureVerifier =
            new FakeManifestSignatureVerifier();

        var provider =
            new GitHubReleaseProvider(
                client,
                new InstallerManifestReader(),
                signatureVerifier,
                new GitHubReleaseProviderOptions(
                    new Uri(
                        "https://api.github.test/"),
                    "turkuaz",
                    "example",
                    ProviderTestData.ManifestAssetName,
                    "TurkuazInstaller-Test/1.1.0"));

        var release =
            await provider.GetLatestReleaseAsync(
                PackageId.Parse(
                    ProviderTestData.PackageId),
                ReleaseChannel.Stable,
                CancellationToken.None);

        Assert.Equal(
            "4.0.0",
            release?.Version.ToString());

        Assert.Equal(
            1,
            signatureVerifier.CallCount);
    }
}
