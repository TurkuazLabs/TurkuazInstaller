// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsBackgroundUpdatePolicyReaderTests.cs
// 📌 Amac: background-updates.json policy parserinin disabled default, valid config ve fail-closed validation davranisini test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Missing file, interval, duplicate entry, unknown field ve secret-bearing HTTPS source rejection senaryolarini kapsar
//
// Bagimli Oldugu Katman: Tool | Config | Service

using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsBackgroundUpdatePolicyReaderTests
{
    [Fact]
    public void Read_MissingConfig_ReturnsDisabledPolicy()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid().ToString("N"),
                "background-updates.json");

        var policy =
            new WindowsBackgroundUpdatePolicyReader(
                path)
                .Read();

        Assert.False(
            policy.Enabled);

        Assert.Empty(
            policy.Entries);

        Assert.Equal(
            TimeSpan.FromMinutes(60),
            policy.Interval);
    }

    [Fact]
    public void Read_EnabledPolicy_MapsEntries()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "enabled": true,
  "interval_minutes": 30,
  "entries": [
    {
      "package_id": "example-app",
      "channel": "stable",
      "manifest_source": "https://updates.example.test/example/installer-manifest.yml"
    },
    {
      "package_id": "beta-app",
      "channel": "beta",
      "manifest_source": "C:\\Packages\\Beta\\installer-manifest.yml"
    }
  ]
}
""");

        try
        {
            var policy =
                new WindowsBackgroundUpdatePolicyReader(
                    path)
                    .Read();

            Assert.True(
                policy.Enabled);

            Assert.Equal(
                TimeSpan.FromMinutes(30),
                policy.Interval);

            Assert.Equal(
                2,
                policy.Entries.Count);

            Assert.Equal(
                "example-app",
                policy.Entries[0]
                    .PackageId.Value);

            Assert.Equal(
                ReleaseChannel.Beta,
                policy.Entries[1]
                    .Channel);
        }
        finally
        {
            DeleteRoot(
                path);
        }
    }

    [Theory]
    [InlineData(14)]
    [InlineData(1441)]
    public void Read_InvalidInterval_Rejects(
        int minutes)
    {
        var path =
            WriteConfig(
                $$"""
{
  "schema_version": 1,
  "enabled": true,
  "interval_minutes": {{minutes}},
  "entries": []
}
""");

        try
        {
            Assert.Throws<InvalidDataException>(
                () =>
                    new WindowsBackgroundUpdatePolicyReader(
                        path)
                        .Read());
        }
        finally
        {
            DeleteRoot(
                path);
        }
    }

    [Fact]
    public void Read_DuplicatePackageChannel_Rejects()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "enabled": true,
  "entries": [
    {
      "package_id": "example-app",
      "channel": "stable",
      "manifest_source": "https://updates.example.test/a.yml"
    },
    {
      "package_id": "example-app",
      "channel": "stable",
      "manifest_source": "https://updates.example.test/b.yml"
    }
  ]
}
""");

        try
        {
            Assert.Throws<InvalidDataException>(
                () =>
                    new WindowsBackgroundUpdatePolicyReader(
                        path)
                        .Read());
        }
        finally
        {
            DeleteRoot(
                path);
        }
    }

    [Fact]
    public void Read_SecretBearingHttpsSource_Rejects()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "enabled": true,
  "entries": [
    {
      "package_id": "example-app",
      "channel": "stable",
      "manifest_source": "https://updates.example.test/manifest.yml?token=secret"
    }
  ]
}
""");

        try
        {
            Assert.Throws<InvalidDataException>(
                () =>
                    new WindowsBackgroundUpdatePolicyReader(
                        path)
                        .Read());
        }
        finally
        {
            DeleteRoot(
                path);
        }
    }

    [Fact]
    public void Read_UnknownProperty_Rejects()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "enabled": false,
  "auto_install": true
}
""");

        try
        {
            Assert.Throws<InvalidDataException>(
                () =>
                    new WindowsBackgroundUpdatePolicyReader(
                        path)
                        .Read());
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
                "background-updates.json");

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
