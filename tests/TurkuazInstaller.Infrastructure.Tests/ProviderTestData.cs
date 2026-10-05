// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/ProviderTestData.cs
// 📌 Amac: Provider contract testlerinde ortak Stable v1 manifest ve digest verilerini tek noktada tanimlar
// 📌 Modul - Test Config CSharp
// Version: 1.1.0
// Aciklama: Install policy, prerequisite, preserve path, rollback ve Authenticode alanlarini parser testlerine saglar
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Tests;

internal static class ProviderTestData
{
    public const string PackageId =
        "example-app";

    public const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    public const string ManifestAssetName =
        "installer-manifest.yml";

    public static string Manifest(
        ReleaseChannel channel,
        string version)
    {
        var channelText =
            channel == ReleaseChannel.Stable
                ? "stable"
                : "beta";

        return $$"""
schema_version: 1
package:
  id: {{PackageId}}
  version: {{version}}
  channel: {{channelText}}
artifact:
  uri: https://downloads.example.invalid/{{PackageId}}/{{version}}/Example-Setup.exe
  sha256: {{Digest}}
  size_bytes: 1024
  signature:
    algorithm: authenticode
    publisher_subject: "CN=Example Software"
install:
  mode: full
  target: C:/Apps/Example
  prerequisites:
    - id: windows-build
      version: ">=17763"
  preserve_paths:
    - UserData
rollback:
  supported: true
  previous_version_required: true
""";
    }
}
