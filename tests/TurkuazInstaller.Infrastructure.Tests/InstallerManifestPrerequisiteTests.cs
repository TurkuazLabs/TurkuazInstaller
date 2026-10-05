// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/InstallerManifestPrerequisiteTests.cs
// 📌 Amac: Manifest prerequisite auto-install policy parsing ve signature zorunlulugunu unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.1
// Aciklama: Signed installer artifact, argument/elevation mapping ve unsigned auto-install fail-closed davranisini kapsar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class InstallerManifestPrerequisiteTests
{
    private const string PrerequisiteDigest =
        "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    private const string SignatureBlock =
        """
          signature:
            algorithm: authenticode
            publisher_subject: "CN=Microsoft Corporation"
""";

    private const string SignedManifest =
        """
schema_version: 1
package:
  id: example-app
  version: 2.4.0
  channel: stable
artifact:
  uri: https://downloads.example.invalid/example-app/2.4.0/Example-Setup.exe
  sha256: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
  size_bytes: 1024
install:
  mode: full
  target: C:/Apps/Example
  prerequisites:
    - id: dotnet-desktop-runtime
      version: ">=10.0.0"
      install:
        artifact:
          uri: https://downloads.example.invalid/windowsdesktop-runtime.exe
          sha256: fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210
          size_bytes: 2048
          signature:
            algorithm: authenticode
            publisher_subject: "CN=Microsoft Corporation"
        arguments:
          - "/install"
          - "/quiet"
          - "/norestart"
        requires_elevation: true
  preserve_paths:
    - UserData
rollback:
  supported: true
  previous_version_required: true
""";

    [Fact]
    public void Read_MapsPrerequisiteAutoInstallPolicy()
    {
        var reader =
            new InstallerManifestReader();

        var release =
            reader.Read(
                SignedManifest);

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

        var unsignedManifest =
            SignedManifest.Replace(
                SignatureBlock,
                string.Empty,
                StringComparison.Ordinal);

        Assert.Throws<FormatException>(
            () =>
                reader.Read(
                    unsignedManifest));
    }
}
