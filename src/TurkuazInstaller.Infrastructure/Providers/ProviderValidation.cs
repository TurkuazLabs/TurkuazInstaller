// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/ProviderValidation.cs
// 📌 Amac: Provider options ve manifest sonuc dogrulamalarini ortak noktada toplar
// 📌 Modul - Tool CSharp
// Version: 0.4.1
// Aciklama: HTTPS URI, API base URI, zorunlu config ve requested package/channel invariantlarini uygular
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Providers;

internal static class ProviderValidation
{
    public static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    public static Uri HttpsUri(Uri uri, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(uri, parameterName);

        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Remote provider URI must use HTTPS.", parameterName);
        }

        return uri;
    }

    public static Uri HttpsBaseUri(Uri uri, string parameterName)
    {
        var validated = HttpsUri(uri, parameterName);
        var text = validated.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? validated.AbsoluteUri
            : string.Concat(validated.AbsoluteUri, "/");

        return new Uri(text, UriKind.Absolute);
    }

    public static PackageRelease MatchRequest(
        PackageRelease release,
        PackageId requestedPackage,
        ReleaseChannel requestedChannel)
    {
        if (release.PackageId != requestedPackage)
        {
            throw new InvalidDataException("Manifest package id does not match the requested package.");
        }

        if (release.Channel != requestedChannel)
        {
            throw new InvalidDataException("Manifest release channel does not match the requested channel.");
        }

        return release;
    }
}
