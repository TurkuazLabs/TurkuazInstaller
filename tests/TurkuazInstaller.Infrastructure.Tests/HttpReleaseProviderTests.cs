// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/HttpReleaseProviderTests.cs
// 📌 Amac: Generic HTTPS providerin kanal bazli signed manifest secimini contract testiyle dogrular
// 📌 Modul - Test CSharp
// Version: 1.3.0
// Aciklama: Manifest + .p7s fetch, Content-Length olsun/olmasin remote byte limitleri, missing signature ve plain HTTP reddini kapsar
//
// Bagimli Oldugu Katman: Tool | Service

using System.Net;
using System.Text;
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
    public async Task GetLatestReleaseAsync_RejectsOversizedRemoteManifestWithoutContentLength()
    {
        var signatureUri =
            string.Concat(
                StableUri.AbsoluteUri,
                ManifestSignatureConventions.DetachedSignatureSuffix);

        using var client =
            new HttpClient(
                new UnknownLengthManifestHandler(
                    StableUri.AbsoluteUri,
                    signatureUri));

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

    private sealed class UnknownLengthManifestHandler
        : HttpMessageHandler
    {
        private readonly string _manifestUri;
        private readonly string _signatureUri;

        public UnknownLengthManifestHandler(
            string manifestUri,
            string signatureUri)
        {
            _manifestUri = manifestUri;
            _signatureUri = signatureUri;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri =
                request.RequestUri?.AbsoluteUri
                ?? string.Empty;

            if (
                string.Equals(
                    uri,
                    _manifestUri,
                    StringComparison.Ordinal))
            {
                return Task.FromResult(
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new UnknownLengthContent(
                                ManifestContentLimits.MaximumManifestBytes +
                                1)
                    });
            }

            if (
                string.Equals(
                    uri,
                    _signatureUri,
                    StringComparison.Ordinal))
            {
                return Task.FromResult(
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new StringContent(
                                ProviderTestData.DetachedSignature,
                                Encoding.UTF8)
                    });
            }

            return Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.NotFound));
        }
    }

    private sealed class UnknownLengthContent
        : HttpContent
    {
        private readonly byte[] _content;

        public UnknownLengthContent(
            int byteCount)
        {
            _content =
                new byte[
                    byteCount];
        }

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            await stream
                .WriteAsync(
                    _content)
                .ConfigureAwait(false);
        }

        protected override bool TryComputeLength(
            out long length)
        {
            length = 0;
            return false;
        }
    }

}
