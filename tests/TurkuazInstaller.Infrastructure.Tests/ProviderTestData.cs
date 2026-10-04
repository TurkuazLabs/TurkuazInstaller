// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/ProviderTestData.cs
// 📌 Amac: Provider contract testlerinde ortak manifest ve digest verilerini tek noktada tanimlar
// 📌 Modul - Test Config CSharp
// Version: 0.4.0
// Aciklama: Test magic string tekrarini azaltan sabit veri ureticisidir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Tests;

internal static class ProviderTestData
{
    public const string PackageId = "example-app";
    public const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    public const string ManifestAssetName = "installer-manifest.yml";

    public static string Manifest(ReleaseChannel channel, string version)
    {
        var channelText = channel == ReleaseChannel.Stable ? "stable" : "beta";

        return $"""
schema_version: 1
package:
  id: {PackageId}
  version: {version}
  channel: {channelText}
artifact:
  uri: https://downloads.example.invalid/{PackageId}/{version}/package.zip
  sha256: {Digest}
  size_bytes: 1024
install:
  mode: full
  target: C:/Apps/Example
rollback:
  supported: true
""";
    }
}
