// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/InstallerManifestReaderTests.cs
// 📌 Amac: YAML installer manifest parserinin typed Domain sonucunu dogrular
// 📌 Modul - Test CSharp
// Version: 0.4.0
// Aciklama: Package, SemVer, channel ve SHA-256 alanlarinin contract uyumunu test eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class InstallerManifestReaderTests
{
    [Fact]
    public void Read_MapsContractFields()
    {
        var reader = new InstallerManifestReader();

        var release = reader.Read(ProviderTestData.Manifest(ReleaseChannel.Stable, "2.4.0"));

        Assert.Equal(ProviderTestData.PackageId, release.PackageId.Value);
        Assert.Equal("2.4.0", release.Version.ToString());
        Assert.Equal(ReleaseChannel.Stable, release.Channel);
        Assert.Equal(ProviderTestData.Digest, release.Artifact.Digest.Sha256);
    }
}
