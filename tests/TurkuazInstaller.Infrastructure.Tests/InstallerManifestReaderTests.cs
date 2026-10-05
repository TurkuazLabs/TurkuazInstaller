// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/InstallerManifestReaderTests.cs
// 📌 Amac: YAML installer manifest parserinin Stable v1 typed Domain sonucunu dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Package, artifact, signature, install policy, prerequisite, preserve path ve rollback alanlarini test eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Prerequisites;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class InstallerManifestReaderTests
{
    [Fact]
    public void Read_MapsStableV1ContractFields()
    {
        var reader =
            new InstallerManifestReader();

        var release =
            reader.Read(
                ProviderTestData.Manifest(
                    ReleaseChannel.Stable,
                    "2.4.0"));

        Assert.Equal(
            ProviderTestData.PackageId,
            release.PackageId.Value);

        Assert.Equal(
            "2.4.0",
            release.Version.ToString());

        Assert.Equal(
            ReleaseChannel.Stable,
            release.Channel);

        Assert.Equal(
            ProviderTestData.Digest,
            release.Artifact.Digest.Sha256);

        Assert.NotNull(
            release.Artifact.Signature);

        Assert.Equal(
            ArtifactSignatureAlgorithm.Authenticode,
            release.Artifact.Signature.Algorithm);

        Assert.Equal(
            "CN=Example Software",
            release.Artifact.Signature.PublisherSubject);

        Assert.Equal(
            PackageInstallMode.Full,
            release.Install.Mode);

        Assert.Equal(
            "C:/Apps/Example",
            release.Install.DefaultTargetPath);

        var prerequisite =
            Assert.Single(
                release.Install.Prerequisites);

        Assert.Equal(
            PrerequisiteIds.WindowsBuild,
            prerequisite.Id);

        Assert.Equal(
            ">=17763",
            prerequisite.VersionExpression);

        Assert.Equal(
            "UserData",
            Assert.Single(
                release.Install.PreservePaths));

        Assert.True(
            release.Rollback.Supported);

        Assert.True(
            release.Rollback.PreviousVersionRequired);
    }

    [Fact]
    public void Read_RejectsAuthenticodeWithoutPublisherSubject()
    {
        var reader =
            new InstallerManifestReader();

        var yaml =
            ProviderTestData
                .Manifest(
                    ReleaseChannel.Stable,
                    "2.4.0")
                .Replace(
                    "    publisher_subject: \"CN=Example Software\"",
                    string.Empty,
                    StringComparison.Ordinal);

        Assert.Throws<FormatException>(
            () => reader.Read(yaml));
    }

    [Fact]
    public void Read_RejectsUnsupportedInstallMode()
    {
        var reader =
            new InstallerManifestReader();

        var yaml =
            ProviderTestData
                .Manifest(
                    ReleaseChannel.Stable,
                    "2.4.0")
                .Replace(
                    "mode: full",
                    "mode: delta",
                    StringComparison.Ordinal);

        Assert.Throws<FormatException>(
            () => reader.Read(yaml));
    }
}
