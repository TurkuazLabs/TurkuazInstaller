// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Networking/NetworkProxyOptions.cs
// 📌 Amac: HTTP client proxy politikasini immutable typed config olarak tasir
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: System/direct/custom mod, custom HTTP proxy URI, local bypass ve explicit Windows default credential tercihini tasir
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Contracts.Networking;

public sealed record NetworkProxyOptions
{
    public NetworkProxyOptions(
        NetworkProxyMode mode,
        Uri? customProxyUri = null,
        bool bypassLocal = true,
        bool useDefaultCredentials = false)
    {
        if (mode == NetworkProxyMode.Custom)
        {
            if (
                customProxyUri is null ||
                !customProxyUri.IsAbsoluteUri ||
                !string.Equals(
                    customProxyUri.Scheme,
                    Uri.UriSchemeHttp,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(
                    customProxyUri.UserInfo) ||
                !string.IsNullOrEmpty(
                    customProxyUri.Query) ||
                !string.IsNullOrEmpty(
                    customProxyUri.Fragment) ||
                customProxyUri.AbsolutePath != "/")
            {
                throw new ArgumentException(
                    "Custom proxy URI must be an absolute HTTP origin without userinfo, path, query or fragment.",
                    nameof(customProxyUri));
            }
        }
        else if (customProxyUri is not null)
        {
            throw new ArgumentException(
                "Custom proxy URI is only valid in custom mode.",
                nameof(customProxyUri));
        }

        if (
            mode == NetworkProxyMode.Direct &&
            useDefaultCredentials)
        {
            throw new ArgumentException(
                "Proxy credentials cannot be enabled in direct mode.",
                nameof(useDefaultCredentials));
        }

        Mode = mode;
        CustomProxyUri = customProxyUri;
        BypassLocal = bypassLocal;
        UseDefaultCredentials = useDefaultCredentials;
    }

    public NetworkProxyMode Mode { get; }

    public Uri? CustomProxyUri { get; }

    public bool BypassLocal { get; }

    public bool UseDefaultCredentials { get; }

    public static NetworkProxyOptions SystemDefault()
    {
        return new NetworkProxyOptions(
            NetworkProxyMode.System);
    }
}
