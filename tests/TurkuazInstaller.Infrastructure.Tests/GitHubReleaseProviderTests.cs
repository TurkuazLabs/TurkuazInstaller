// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/GitHubReleaseProviderTests.cs
// 📌 Amac: GitHub providerin stable release assetinden signed manifest bulma davranisini dogrular
// 📌 Modul - Test CSharp
// Version: 1.2.0
// Aciklama: Public trust davranisina ek olarak private Bearer auth ve allow-list disi host token sizintisi engelini test eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Credentials;
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
    [Fact]
    public async Task GetLatestReleaseAsync_PrivateRelease_AuthorizesApiManifestAndSignature()
    {
        const string token =
            "github-private-token";

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

        var handler =
            new StubHttpMessageHandler(
                new Dictionary<string, string>
                {
                    [apiUrl] =
                        releaseJson,
                    [manifestUrl] =
                        ProviderTestData.Manifest(
                            ReleaseChannel.Stable,
                            "4.1.0"),
                    [signatureUrl] =
                        ProviderTestData.DetachedSignature
                });

        using var client =
            new HttpClient(
                handler);

        var credentialResolver =
            new FakeProviderCredentialResolver(
                token);

        var provider =
            new GitHubReleaseProvider(
                client,
                new InstallerManifestReader(),
                new FakeManifestSignatureVerifier(),
                new GitHubReleaseProviderOptions(
                    new Uri(
                        "https://api.github.test/"),
                    "turkuaz",
                    "example",
                    ProviderTestData.ManifestAssetName,
                    "TurkuazInstaller-Test/1.2.0",
                    new[]
                    {
                        new Uri(
                            "https://downloads.github.test/")
                    }),
                credentialResolver);

        var release =
            await provider.GetLatestReleaseAsync(
                PackageId.Parse(
                    ProviderTestData.PackageId),
                ReleaseChannel.Stable,
                CancellationToken.None);

        Assert.Equal(
            "4.1.0",
            release?.Version.ToString());

        Assert.Equal(
            1,
            credentialResolver.CallCount);

        Assert.Equal(
            ProviderCredentialProvider.GitHub,
            credentialResolver.LastRequest?.Provider);

        Assert.Equal(
            "api.github.test",
            credentialResolver.LastRequest?.Authority);

        Assert.Equal(
            3,
            handler.Requests.Count);

        Assert.All(
            handler.Requests,
            request =>
            {
                Assert.Equal(
                    "Bearer",
                    request.AuthorizationScheme);

                Assert.Equal(
                    token,
                    request.AuthorizationParameter);
            });
    }

    [Fact]
    public async Task GetLatestReleaseAsync_AssetOutsideCredentialAllowList_DoesNotLeakToken()
    {
        const string token =
            "github-private-token";

        const string apiUrl =
            "https://api.github.test/repos/turkuaz/example/releases/latest";

        const string manifestUrl =
            "https://untrusted-cdn.test/installer-manifest.yml";

        var signatureUrl =
            string.Concat(
                manifestUrl,
                ManifestSignatureConventions.DetachedSignatureSuffix);

        var releaseJson =
            $$"""
{"draft":false,"prerelease":false,"assets":[{"name":"{{ProviderTestData.ManifestAssetName}}","browser_download_url":"{{manifestUrl}}"}]}
""";

        var handler =
            new StubHttpMessageHandler(
                new Dictionary<string, string>
                {
                    [apiUrl] =
                        releaseJson,
                    [manifestUrl] =
                        ProviderTestData.Manifest(
                            ReleaseChannel.Stable,
                            "4.2.0"),
                    [signatureUrl] =
                        ProviderTestData.DetachedSignature
                });

        using var client =
            new HttpClient(
                handler);

        var provider =
            new GitHubReleaseProvider(
                client,
                new InstallerManifestReader(),
                new FakeManifestSignatureVerifier(),
                new GitHubReleaseProviderOptions(
                    new Uri(
                        "https://api.github.test/"),
                    "turkuaz",
                    "example",
                    ProviderTestData.ManifestAssetName,
                    "TurkuazInstaller-Test/1.2.0"),
                new FakeProviderCredentialResolver(
                    token));

        var release =
            await provider.GetLatestReleaseAsync(
                PackageId.Parse(
                    ProviderTestData.PackageId),
                ReleaseChannel.Stable,
                CancellationToken.None);

        Assert.Equal(
            "4.2.0",
            release?.Version.ToString());

        var apiRequest =
            Assert.Single(
                handler.Requests,
                request =>
                    request.Uri == apiUrl);

        Assert.Equal(
            "Bearer",
            apiRequest.AuthorizationScheme);

        Assert.Equal(
            token,
            apiRequest.AuthorizationParameter);

        Assert.All(
            handler.Requests.Where(
                request =>
                    request.Uri != apiUrl),
            request =>
            {
                Assert.Null(
                    request.AuthorizationScheme);

                Assert.Null(
                    request.AuthorizationParameter);
            });
    }

}
