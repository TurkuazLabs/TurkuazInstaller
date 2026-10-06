// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/GiteaReleaseProviderTests.cs
// 📌 Amac: Gitea providerin beta release assetinden signed manifest bulma davranisini dogrular
// 📌 Modul - Test CSharp
// Version: 1.2.0
// Aciklama: Public trust davranisina ek olarak private Gitea token headerinin API, manifest ve .p7s isteklerine uygulanmasini test eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Credentials;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.Gitea;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class GiteaReleaseProviderTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_LoadsVerifiedBetaManifestAsset()
    {
        const string apiUrl =
            "https://gitea.example.test/api/v1/repos/turkuaz/example/releases";

        const string manifestUrl =
            "https://gitea.example.test/downloads/installer-manifest.yml";

        var signatureUrl =
            string.Concat(
                manifestUrl,
                ManifestSignatureConventions.DetachedSignatureSuffix);

        var releaseJson =
            $$"""
[{"draft":false,"prerelease":true,"assets":[{"name":"{{ProviderTestData.ManifestAssetName}}","browser_download_url":"{{manifestUrl}}"}]}]
""";

        var handler =
            new StubHttpMessageHandler(
                new Dictionary<string, string>
                {
                    [apiUrl] =
                        releaseJson,
                    [manifestUrl] =
                        ProviderTestData.Manifest(
                            ReleaseChannel.Beta,
                            "5.0.0-beta.1"),
                    [signatureUrl] =
                        ProviderTestData.DetachedSignature
                });

        using var client =
            new HttpClient(
                handler);

        var signatureVerifier =
            new FakeManifestSignatureVerifier();

        var provider =
            new GiteaReleaseProvider(
                client,
                new InstallerManifestReader(),
                signatureVerifier,
                new GiteaReleaseProviderOptions(
                    new Uri(
                        "https://gitea.example.test/api/v1/"),
                    "turkuaz",
                    "example",
                    ProviderTestData.ManifestAssetName));

        var release =
            await provider.GetLatestReleaseAsync(
                PackageId.Parse(
                    ProviderTestData.PackageId),
                ReleaseChannel.Beta,
                CancellationToken.None);

        Assert.Equal(
            "5.0.0-beta.1",
            release?.Version.ToString());

        Assert.Equal(
            1,
            signatureVerifier.CallCount);

        Assert.All(
            handler.Requests,
            request =>
            {
                Assert.Null(
                    request.AuthorizationScheme);

                Assert.Null(
                    request.AuthorizationParameter);
            });
    }

    [Fact]
    public async Task GetLatestReleaseAsync_PrivateRelease_AuthorizesApiManifestAndSignature()
    {
        const string token =
            "gitea-private-token";

        const string apiUrl =
            "https://gitea.example.test/api/v1/repos/turkuaz/example/releases";

        const string manifestUrl =
            "https://gitea.example.test/downloads/installer-manifest.yml";

        var signatureUrl =
            string.Concat(
                manifestUrl,
                ManifestSignatureConventions.DetachedSignatureSuffix);

        var releaseJson =
            $$"""
[{"draft":false,"prerelease":false,"assets":[{"name":"{{ProviderTestData.ManifestAssetName}}","browser_download_url":"{{manifestUrl}}"}]}]
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
                            "5.1.0"),
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
            new GiteaReleaseProvider(
                client,
                new InstallerManifestReader(),
                new FakeManifestSignatureVerifier(),
                new GiteaReleaseProviderOptions(
                    new Uri(
                        "https://gitea.example.test/api/v1/"),
                    "turkuaz",
                    "example",
                    ProviderTestData.ManifestAssetName),
                credentialResolver);

        var release =
            await provider.GetLatestReleaseAsync(
                PackageId.Parse(
                    ProviderTestData.PackageId),
                ReleaseChannel.Stable,
                CancellationToken.None);

        Assert.Equal(
            "5.1.0",
            release?.Version.ToString());

        Assert.Equal(
            1,
            credentialResolver.CallCount);

        Assert.Equal(
            ProviderCredentialProvider.Gitea,
            credentialResolver.LastRequest?.Provider);

        Assert.Equal(
            "gitea.example.test",
            credentialResolver.LastRequest?.Authority);

        Assert.Equal(
            3,
            handler.Requests.Count);

        Assert.All(
            handler.Requests,
            request =>
            {
                Assert.Equal(
                    "token",
                    request.AuthorizationScheme);

                Assert.Equal(
                    token,
                    request.AuthorizationParameter);
            });
    }

}
