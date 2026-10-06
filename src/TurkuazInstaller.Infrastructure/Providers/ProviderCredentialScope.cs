// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/ProviderCredentialScope.cs
// 📌 Amac: Provider access tokeninin gidebilecegi HTTPS authority allow-listini normalize eder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: API ve explicit asset originlerini authority bazinda tekilleştirir; HTTP veya malformed originleri reddeder
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Infrastructure.Providers;

internal static class ProviderCredentialScope
{
    public static IReadOnlySet<string> Create(
        Uri primaryOrigin,
        IReadOnlyList<Uri>? additionalOrigins = null)
    {
        ArgumentNullException.ThrowIfNull(
            primaryOrigin);

        var authorities =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                NormalizeAuthority(
                    primaryOrigin)
            };

        if (additionalOrigins is not null)
        {
            foreach (var origin in additionalOrigins)
            {
                authorities.Add(
                    NormalizeAuthority(
                        origin));
            }
        }

        return authorities;
    }

    private static string NormalizeAuthority(
        Uri origin)
    {
        ArgumentNullException.ThrowIfNull(
            origin);

        if (
            !origin.IsAbsoluteUri ||
            !string.Equals(
                origin.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                origin.AbsolutePath,
                "/",
                StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(
                origin.Query) ||
            !string.IsNullOrEmpty(
                origin.Fragment))
        {
            throw new ArgumentException(
                "Provider credential origin must be an HTTPS origin without path, query or fragment.",
                nameof(origin));
        }

        return origin.Authority
            .ToLowerInvariant();
    }
}
