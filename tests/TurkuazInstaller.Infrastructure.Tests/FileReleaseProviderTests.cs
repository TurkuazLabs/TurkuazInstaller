// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/FileReleaseProviderTests.cs
// 📌 Amac: Local file providerin beta manifestini diskten okuyabildigini dogrular
// 📌 Modul - Test CSharp
// Version: 0.4.0
// Aciklama: Air-gapped provider senaryosunu gecici dosya ile contract testi olarak kapsar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.File;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class FileReleaseProviderTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_LoadsBetaManifest()
    {
        var stablePath = Path.GetTempFileName();
        var betaPath = Path.GetTempFileName();

        try
        {
            await System.IO.File.WriteAllTextAsync(betaPath, ProviderTestData.Manifest(ReleaseChannel.Beta, "3.0.0-beta.1"));

            var provider = new FileReleaseProvider(
                new InstallerManifestReader(),
                new FileReleaseProviderOptions(stablePath, betaPath));

            var release = await provider.GetLatestReleaseAsync(
                PackageId.Parse(ProviderTestData.PackageId),
                ReleaseChannel.Beta,
                CancellationToken.None);

            Assert.Equal("3.0.0-beta.1", release?.Version.ToString());
        }
        finally
        {
            System.IO.File.Delete(stablePath);
            System.IO.File.Delete(betaPath);
        }
    }
}
