// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/InstallerManifestPrerequisiteTests.cs
// 📌 Amac: Manifest prerequisite auto-install policy parsing ve signature zorunlulugunu unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Signed installer artifact, argument/elevation mapping ve unsigned auto-install fail-closed davranisini kapsar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class InstallerManifestPrerequisiteTests
{
    private const string PrerequisiteDigest =
        "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    [Fact]
    public void Read_MapsPrerequisiteAutoInstallPolicy()
    {
        var reader =
            new InstallerManifestReader();

        var release =
            reader.Read(
                AddAutoInstallPolicy(
                    ProviderTestData.Manifest(
                        ReleaseChannel.Stable,
                        "2.4.0"),
                    includeSignature: true));

        var prerequisite =
            Assert.Single(
                release.Install.Prerequisites);

        var installAction =
            Assert.IsType<TurkuazInstaller.Domain.Prerequisites.PrerequisiteInstallAction>(
                prerequisite.InstallAction);

        Assert.Equal(
            "https://downloads.example.invalid/windowsdesktop-runtime.exe",
            installAction.Artifact.Uri.AbsoluteUri);

        Assert.Equal(
            PrerequisiteDigest,
            installAction.Artifact.Digest.Sha256);

        Assert.NotNull(
            installAction.Artifact.Signature);

        Assert.Equal(
            ArtifactSignatureAlgorithm.Authenticode,
            installAction.Artifact.Signature!.Algorithm);

        Assert.Equal(
            "CN=Microsoft Corporation",
            installAction.Artifact.Signature.PublisherSubject);

        Assert.Equal(
            new[]
            {
                "/install",
                "/quiet",
                "/norestart"
            },
            installAction.Arguments);

        Assert.True(
            installAction.RequiresElevation);
    }

    [Fact]
    public void Read_RejectsUnsignedPrerequisiteAutoInstallArtifact()
    {
        var reader =
            new InstallerManifestReader();

        var yaml =
            AddAutoInstallPolicy(
                ProviderTestData.Manifest(
                    ReleaseChannel.Stable,
                    "2.4.0"),
                includeSignature: false);

        Assert.Throws<FormatException>(
            () =>
                reader.Read(
                    yaml));
    }

    private static string AddAutoInstallPolicy(
        string manifest,
        bool includeSignature)
    {
        const string prerequisite =
            """
    - id: windows-build
      version: ">=17763"
""";

        var signature =
            includeSignature
                ? """
        signature:
          algorithm: authenticode
          publisher_subject: "CN=Microsoft Corporation"
"""
                : string.Empty;

        var replacement =
            string.Concat(
                prerequisite.TrimEnd(),
                Environment.NewLine,
                """
      install:
        artifact:
          uri: https://downloads.example.invalid/windowsdesktop-runtime.exe
          sha256: fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210
          size_bytes: 2048
""",
                Environment.NewLine,
                signature,
                """
        arguments:
          - "/install"
          - "/quiet"
          - "/norestart"
        requires_elevation: true
""",
                Environment.NewLine);

        return manifest.Replace(
            prerequisite,
            replacement,
            StringComparison.Ordinal);
    }
}
