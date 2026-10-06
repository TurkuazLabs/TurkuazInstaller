// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsVersionUpdatePolicyRepositoryTests.cs
// 📌 Amac: version-policy.json repository missing/valid/duplicate/no-op validation davranisini test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Maximum version, skipped version, duplicate identity ve strict config kurallarini gercek temp JSON dosyasinda dogrular
//
// Bagimli Oldugu Katman: Repo | Config | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsVersionUpdatePolicyRepositoryTests
{
    [Fact]
    public async Task GetAsync_MissingConfig_ReturnsNull()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid().ToString("N"),
                "version-policy.json");

        var repository =
            new WindowsVersionUpdatePolicyRepository(
                path);

        var policy =
            await repository.GetAsync(
                PackageId.Parse(
                    "example-app"),
                ReleaseChannel.Stable,
                CancellationToken.None);

        Assert.Null(
            policy);
    }

    [Fact]
    public async Task GetAsync_ValidPolicy_MapsMaximumAndSkippedVersions()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "entries": [
    {
      "package_id": "example-app",
      "channel": "stable",
      "maximum_version": "2.5.0",
      "skipped_versions": ["2.4.1", "2.4.2"]
    }
  ]
}
""");

        try
        {
            var repository =
                new WindowsVersionUpdatePolicyRepository(
                    path);

            var policy =
                await repository.GetAsync(
                    PackageId.Parse(
                        "example-app"),
                    ReleaseChannel.Stable,
                    CancellationToken.None);

            Assert.NotNull(
                policy);

            Assert.Equal(
                "2.5.0",
                policy!.MaximumVersion?.ToString());

            Assert.Equal(
                new[]
                {
                    "2.4.1",
                    "2.4.2"
                },
                policy.SkippedVersions
                    .Select(
                        version =>
                            version.ToString())
                    .ToArray());
        }
        finally
        {
            DeleteRoot(
                path);
        }
    }

    [Fact]
    public async Task GetAsync_DuplicatePackageChannel_Rejects()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "entries": [
    {
      "package_id": "example-app",
      "channel": "stable",
      "maximum_version": "2.0.0"
    },
    {
      "package_id": "example-app",
      "channel": "stable",
      "skipped_versions": ["2.1.0"]
    }
  ]
}
""");

        try
        {
            var repository =
                new WindowsVersionUpdatePolicyRepository(
                    path);

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    repository.GetAsync(
                        PackageId.Parse(
                            "example-app"),
                        ReleaseChannel.Stable,
                        CancellationToken.None));
        }
        finally
        {
            DeleteRoot(
                path);
        }
    }

    [Fact]
    public async Task GetAsync_NoOpEntry_Rejects()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "entries": [
    {
      "package_id": "example-app",
      "channel": "stable"
    }
  ]
}
""");

        try
        {
            var repository =
                new WindowsVersionUpdatePolicyRepository(
                    path);

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    repository.GetAsync(
                        PackageId.Parse(
                            "example-app"),
                        ReleaseChannel.Stable,
                        CancellationToken.None));
        }
        finally
        {
            DeleteRoot(
                path);
        }
    }

    private static string WriteConfig(
        string content)
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "version-policy.json");

        File.WriteAllText(
            path,
            content);

        return path;
    }

    private static void DeleteRoot(
        string path)
    {
        var root =
            Path.GetDirectoryName(
                path);

        if (
            root is not null &&
            Directory.Exists(
                root))
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
