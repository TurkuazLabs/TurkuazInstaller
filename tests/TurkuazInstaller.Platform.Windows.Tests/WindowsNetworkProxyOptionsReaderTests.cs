// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsNetworkProxyOptionsReaderTests.cs
// 📌 Amac: network.json proxy config parserinin default, custom ve fail-closed davranislarini test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Missing config, direct/custom mod, unknown field, userinfo ve direct credential rejection kurallarini kapsar
//
// Bagimli Oldugu Katman: Tool | Config

using TurkuazInstaller.Contracts.Networking;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsNetworkProxyOptionsReaderTests
{
    [Fact]
    public void Read_MissingConfig_UsesSystemProxy()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid().ToString("N"),
                "network.json");

        var options =
            new WindowsNetworkProxyOptionsReader(
                path)
                .Read();

        Assert.Equal(
            NetworkProxyMode.System,
            options.Mode);

        Assert.Null(
            options.CustomProxyUri);

        Assert.False(
            options.UseDefaultCredentials);
    }

    [Fact]
    public void Read_CustomProxy_MapsTypedOptions()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "mode": "custom",
  "custom_proxy": "http://proxy.example.test:8080/",
  "bypass_local": false,
  "use_default_credentials": true
}
""");

        try
        {
            var options =
                new WindowsNetworkProxyOptionsReader(
                    path)
                    .Read();

            Assert.Equal(
                NetworkProxyMode.Custom,
                options.Mode);

            Assert.Equal(
                "http://proxy.example.test:8080/",
                options.CustomProxyUri?.AbsoluteUri);

            Assert.False(
                options.BypassLocal);

            Assert.True(
                options.UseDefaultCredentials);
        }
        finally
        {
            DeleteRoot(
                path);
        }
    }

    [Fact]
    public void Read_UnknownProperty_RejectsConfig()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "mode": "system",
  "password": "must-not-be-accepted"
}
""");

        try
        {
            Assert.Throws<InvalidDataException>(
                () =>
                    new WindowsNetworkProxyOptionsReader(
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
    public void Read_CustomProxyWithUserInfo_RejectsConfig()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "mode": "custom",
  "custom_proxy": "http://user:secret@proxy.example.test:8080/"
}
""");

        try
        {
            Assert.Throws<InvalidDataException>(
                () =>
                    new WindowsNetworkProxyOptionsReader(
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
    public void Read_DirectWithDefaultCredentials_RejectsConfig()
    {
        var path =
            WriteConfig(
                """
{
  "schema_version": 1,
  "mode": "direct",
  "use_default_credentials": true
}
""");

        try
        {
            Assert.Throws<InvalidDataException>(
                () =>
                    new WindowsNetworkProxyOptionsReader(
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
                "network.json");

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
