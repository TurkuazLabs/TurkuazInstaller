// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/HttpReleaseProviderTests.cs
// 📌 Amac: Generic HTTPS providerin kanal bazli manifest secimini contract testiyle dogrular
// 📌 Modul - Test CSharp
// Version: 0.4.0
// Aciklama: Stable manifest fetch ve HTTP transport reddi senaryolarini kapsar
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
    private static readonly Uri StableUri = new("https://feed.example.invalid/stable.yml");
    private static readonly Uri BetaUri = new("https://feed.example.invalid/beta.yml");

    [Fact]
    public async Task GetLatestReleaseAsync_LoadsStableManifest()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(new Dictionary<string, string>
        {
            [StableUri.AbsoluteUri] = ProviderTestData.Manifest(ReleaseChannel.Stable, "2.0.0")
        }));

        var provider = new HttpReleaseProvider(
            client,
            new InstallerManifestReader(),
            new HttpReleaseProviderOptions(StableUri, BetaUri));

        var release = await provider.GetLatestReleaseAsync(
            PackageId.Parse(ProviderTestData.PackageId),
            ReleaseChannel.Stable,
            CancellationToken.None);

        Assert.Equal("2.0.0", release?.Version.ToString());
    }

    [Fact]
    public void Options_RejectPlainHttp()
    {
        Assert.Throws<ArgumentException>(() => new HttpReleaseProviderOptions(
            new Uri("http://feed.example.invalid/stable.yml"),
            BetaUri));
    }
}
