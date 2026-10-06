// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsHttpClientFactory.cs
// 📌 Amac: TurkuazInstaller HTTP clientlerini typed system/direct/custom proxy politikasiyla olusturur
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: System modunda OS proxy davranisini korur, direct modda proxyyi kapatir, custom modda explicit WebProxy uygular
//
// Bagimli Oldugu Katman: Tool | Config

using System.Net;
using TurkuazInstaller.Contracts.Networking;

namespace TurkuazInstaller.Platform.Windows.Tools;

public static class WindowsHttpClientFactory
{
    public static HttpClient Create(
        NetworkProxyOptions options,
        TimeSpan? timeout = null)
    {
        var handler =
            CreateHandler(
                options);

        var client =
            new HttpClient(
                handler,
                disposeHandler: true);

        if (timeout is not null)
        {
            if (timeout <= TimeSpan.Zero)
            {
                client.Dispose();

                throw new ArgumentOutOfRangeException(
                    nameof(timeout));
            }

            client.Timeout =
                timeout.Value;
        }

        return client;
    }

    public static HttpClientHandler CreateHandler(
        NetworkProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        var handler =
            new HttpClientHandler();

        switch (options.Mode)
        {
            case NetworkProxyMode.System:
                handler.UseProxy = true;
                handler.Proxy = null;

                if (options.UseDefaultCredentials)
                {
                    handler.DefaultProxyCredentials =
                        CredentialCache.DefaultCredentials;
                }

                break;

            case NetworkProxyMode.Direct:
                handler.UseProxy = false;
                handler.Proxy = null;
                break;

            case NetworkProxyMode.Custom:
            {
                var proxy =
                    new WebProxy(
                        options.CustomProxyUri!)
                    {
                        BypassProxyOnLocal =
                            options.BypassLocal
                    };

                if (options.UseDefaultCredentials)
                {
                    proxy.Credentials =
                        CredentialCache.DefaultCredentials;
                }

                handler.UseProxy = true;
                handler.Proxy = proxy;
                break;
            }

            default:
                handler.Dispose();

                throw new ArgumentOutOfRangeException(
                    nameof(options));
        }

        return handler;
    }
}
