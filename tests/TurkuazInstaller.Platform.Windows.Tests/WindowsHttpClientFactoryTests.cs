// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsHttpClientFactoryTests.cs
// 📌 Amac: Windows HTTP handler factory system/direct/custom proxy mappingini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: UseProxy, custom WebProxy, local bypass, default credentials ve timeout kontratini dogrular
//
// Bagimli Oldugu Katman: Tool | Config

using System.Net;
using TurkuazInstaller.Contracts.Networking;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsHttpClientFactoryTests
{
    [Fact]
    public void CreateHandler_SystemMode_UsesDefaultProxy()
    {
        using var handler =
            WindowsHttpClientFactory.CreateHandler(
                new NetworkProxyOptions(
                    NetworkProxyMode.System));

        Assert.True(
            handler.UseProxy);

        Assert.Null(
            handler.Proxy);
    }

    [Fact]
    public void CreateHandler_SystemMode_CanUseDefaultCredentials()
    {
        using var handler =
            WindowsHttpClientFactory.CreateHandler(
                new NetworkProxyOptions(
                    NetworkProxyMode.System,
                    useDefaultCredentials: true));

        Assert.True(
            handler.UseProxy);

        Assert.NotNull(
            handler.DefaultProxyCredentials);
    }

    [Fact]
    public void CreateHandler_DirectMode_DisablesProxy()
    {
        using var handler =
            WindowsHttpClientFactory.CreateHandler(
                new NetworkProxyOptions(
                    NetworkProxyMode.Direct));

        Assert.False(
            handler.UseProxy);

        Assert.Null(
            handler.Proxy);
    }

    [Fact]
    public void CreateHandler_CustomMode_UsesExplicitProxy()
    {
        var options =
            new NetworkProxyOptions(
                NetworkProxyMode.Custom,
                new Uri(
                    "http://proxy.example.test:8080/"),
                bypassLocal: false,
                useDefaultCredentials: true);

        using var handler =
            WindowsHttpClientFactory.CreateHandler(
                options);

        Assert.True(
            handler.UseProxy);

        var proxy =
            Assert.IsType<WebProxy>(
                handler.Proxy);

        Assert.Equal(
            options.CustomProxyUri,
            proxy.Address);

        Assert.False(
            proxy.BypassProxyOnLocal);

        Assert.NotNull(
            proxy.Credentials);
    }

    [Fact]
    public void Create_WithTimeout_AppliesTimeout()
    {
        using var client =
            WindowsHttpClientFactory.Create(
                NetworkProxyOptions.SystemDefault(),
                TimeSpan.FromSeconds(
                    17));

        Assert.Equal(
            TimeSpan.FromSeconds(
                17),
            client.Timeout);
    }
}
