// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/HttpReleaseProviderTests.cs
// 📌 Amac: Generic HTTPS providerin kanal bazli signed manifest secimini contract testiyle dogrular
// 📌 Modul - Test CSharp
// Version: 1.2.0
// Aciklama: Manifest + .p7s fetch, verifier cagrisi, remote byte limitleri, missing signature ve plain HTTP reddi senaryolarini kapsar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.Http;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class HttpReleaseProviderTests
{
    private static readonly Uri StableUri =
        new(
            "https://feed.example.invalid/stable.yml");

    private static readonly Uri BetaUri =
        new(
            "https://feed.example.invalid/beta.yml");

    [Fact]
    public async Task GetLatestReleaseAsync_LoadsVerifiedStableManifest()
    {
        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    new Dictionary<string, string>
                    {
                        [StableUri.AbsoluteUri] =
                            ProviderTestData.Manifest(
                                ReleaseChannel.Stable,
                                "2.0.0"),
                        [string.Concat(
                            StableUri.AbsoluteUri,
                            ManifestSignatureConventions.DetachedSignatureSuffix)] =
                            ProviderTestData.DetachedSignature
                    }));

        var signatureVerifier =
            new FakeManifestSignatureVerifier();

        var provider =
            new HttpReleaseProvider(
                client,
                new InstallerManifestReader(),
                signatureVerifier,
                new HttpReleaseProviderOptions(
                    StableUri,
                    BetaUri));

        var release =
            await provider.GetLatestReleaseAsync(
                PackageId.Parse(
                    ProviderTestData.PackageId),
                ReleaseChannel.Stable,
                CancellationToken.None);

        Assert.Equal(
            "2.0.0",
            release?.Version.ToString());

        Assert.Equal(
            1,
            signatureVerifier.CallCount);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsOversizedRemoteManifestBeforeVerification()
    {
        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    new Dictionary<string, string>
                    {
                        [StableUri.AbsoluteUri] =
                            new string(
                                'a',
                                ManifestContentLimits.MaximumManifestBytes +
                                1),
                        [string.Concat(
                            StableUri.AbsoluteUri,
                            ManifestSignatureConventions.DetachedSignatureSuffix)] =
                            ProviderTestData.DetachedSignature
                    }));

        var signatureVerifier =
            new FakeManifestSignatureVerifier();

        var provider =
            new HttpReleaseProvider(
                client,
                new InstallerManifestReader(),
                signatureVerifier,
                new HttpReleaseProviderOptions(
                    StableUri,
                    BetaUri));

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                provider.GetLatestReleaseAsync(
                    PackageId.Parse(
                        ProviderTestData.PackageId),
                    ReleaseChannel.Stable,
                    CancellationToken.None));

        Assert.Equal(
            0,
            signatureVerifier.CallCount);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsOversizedRemoteSignatureBeforeVerification()
    {
        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    new Dictionary<string, string>
                    {
                        [StableUri.AbsoluteUri] =
                            ProviderTestData.Manifest(
                                ReleaseChannel.Stable,
                                "2.0.0"),
                        [string.Concat(
                            StableUri.AbsoluteUri,
                            ManifestSignatureConventions.DetachedSignatureSuffix)] =
                            new string(
                                'a',
                                ManifestContentLimits.MaximumDetachedSignatureBytes +
                                1)
                    }));

        var signatureVerifier =
            new FakeManifestSignatureVerifier();

        var provider =
            new HttpReleaseProvider(
                client,
                new InstallerManifestReader(),
                signatureVerifier,
                new HttpReleaseProviderOptions(
                    StableUri,
                    BetaUri));

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                provider.GetLatestReleaseAsync(
                    PackageId.Parse(
                        ProviderTestData.PackageId),
                    ReleaseChannel.Stable,
                    CancellationToken.None));

        Assert.Equal(
            0,
            signatureVerifier.CallCount);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsMissingDetachedSignature()
    {
        using var client =
            new HttpClient(
                new StubHttpMessageHandler(
                    new Dictionary<string, string>
                    {
                        [StableUri.AbsoluteUri] =
                            ProviderTestData.Manifest(
                                ReleaseChannel.Stable,
                                "2.0.0")
                    }));

        var provider =
            new HttpReleaseProvider(
                client,
                new InstallerManifestReader(),
                new FakeManifestSignatureVerifier(),
                new HttpReleaseProviderOptions(
                    StableUri,
                    BetaUri));

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                provider.GetLatestReleaseAsync(
                    PackageId.Parse(
                        ProviderTestData.PackageId),
                    ReleaseChannel.Stable,
                    CancellationToken.None));
    }

    [Fact]
    public void Options_RejectPlainHttp()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new HttpReleaseProviderOptions(
                    new Uri(
                        "http://feed.example.invalid/stable.yml"),
                    BetaUri));
    }
}
