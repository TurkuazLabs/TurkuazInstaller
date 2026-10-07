// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/InstallerManifestReaderTests.cs
// 📌 Amac: YAML installer manifest parserinin Stable v1 typed Domain sonucunu dogrular
// 📌 Modul - Test CSharp
// Version: 1.3.3
// Aciklama: Package/artifact/install alanlarina optional exact-base delta ve signed Windows policy mapping testlerini ekler
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Integrations;
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
    public void Read_MapsOptionalDeltaArtifact()
    {
        var reader =
            new InstallerManifestReader();

        var yaml =
            ProviderTestData
                .Manifest(
                    ReleaseChannel.Stable,
                    "2.4.0")
                .Replace(
                    "Example-Setup.exe",
                    "Example-2.4.0-full.nupkg",
                    StringComparison.Ordinal)
                .Replace(
                    "    publisher_subject: \"CN=Example Software\"",
                    $"""
    publisher_subject: "CN=Example Software"
  delta:
    from_version: 2.3.0
    uri: https://downloads.example.invalid/{{ProviderTestData.PackageId}}/2.4.0/Example-2.4.0-delta.nupkg
    sha256: {{ProviderTestData.Digest}}
    size_bytes: 256
""",
                    StringComparison.Ordinal);

        var release =
            reader.Read(
                yaml);

        Assert.NotNull(
            release.DeltaArtifact);

        Assert.Equal(
            "2.3.0",
            release.DeltaArtifact.FromVersion.ToString());

        Assert.Equal(
            256,
            release.DeltaArtifact.Artifact.SizeBytes);

        Assert.EndsWith(
            "Example-2.4.0-delta.nupkg",
            release.DeltaArtifact.Artifact.Uri.AbsolutePath,
            StringComparison.Ordinal);
    }


    [Fact]
    public void Read_MapsWindowsIntegrationPolicy()
    {
        var reader =
            new InstallerManifestReader();

        var yaml =
            ProviderTestData
                .Manifest(
                    ReleaseChannel.Stable,
                    "2.5.0")
                .ReplaceLineEndings(
                    "\n")
                .Replace(
                    "  preserve_paths:\n    - UserData",
                    """
  preserve_paths:
    - UserData
  windows:
    shortcuts:
      - id: main
        name: Example App
        location: start_menu
        executable: ExampleApp.exe
      - id: desktop
        name: Example App
        location: desktop
        executable: bin/ExampleApp.exe
    protocols:
      - scheme: example-app
        executable: ExampleApp.exe
""",
                    StringComparison.Ordinal);

        var release =
            reader.Read(
                yaml);

        Assert.Equal(
            2,
            release.Install
                .WindowsIntegration
                .Shortcuts
                .Count);

        Assert.Equal(
            WindowsShortcutLocation.StartMenu,
            release.Install
                .WindowsIntegration
                .Shortcuts[0]
                .Location);

        Assert.Equal(
            "example-app",
            Assert.Single(
                release.Install
                    .WindowsIntegration
                    .Protocols)
                .Scheme);
    }

    [Fact]
    public void Read_RejectsWindowsIntegrationTraversal()
    {
        var reader =
            new InstallerManifestReader();

        var yaml =
            ProviderTestData
                .Manifest(
                    ReleaseChannel.Stable,
                    "2.5.0")
                .ReplaceLineEndings(
                    "\n")
                .Replace(
                    "  preserve_paths:\n    - UserData",
                    """
  preserve_paths:
    - UserData
  windows:
    shortcuts:
      - id: main
        name: Example App
        location: start_menu
        executable: ../ExampleApp.exe
""",
                    StringComparison.Ordinal);

        Assert.Throws<ArgumentException>(
            () =>
                reader.Read(
                    yaml));
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
