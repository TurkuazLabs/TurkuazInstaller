// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/ProviderRequestAuthorization.cs
// 📌 Amac: Provider tokenini yalniz allow-list HTTPS authority isteklerine Authorization headeri olarak uygular
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Secret'in release metadata ile gelen keyfi asset hostlarina sizmasini engeller ve ToString ile token render etmez
//
// Bagimli Oldugu Katman: Tool | Service

using System.Net.Http.Headers;
using TurkuazInstaller.Contracts.Credentials;

namespace TurkuazInstaller.Infrastructure.Providers;

internal sealed class ProviderRequestAuthorization
{
    private readonly ProviderAccessToken _accessToken;
    private readonly string _scheme;
    private readonly IReadOnlySet<string> _allowedAuthorities;

    public ProviderRequestAuthorization(
        ProviderAccessToken accessToken,
        string scheme,
        IReadOnlySet<string> allowedAuthorities)
    {
        ArgumentNullException.ThrowIfNull(
            accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            scheme);
        ArgumentNullException.ThrowIfNull(
            allowedAuthorities);

        if (allowedAuthorities.Count == 0)
        {
            throw new ArgumentException(
                "Provider authorization requires at least one allowed authority.",
                nameof(allowedAuthorities));
        }

        _accessToken =
            accessToken;

        _scheme =
            scheme.Trim();

        _allowedAuthorities =
            allowedAuthorities;
    }

    public void Apply(
        HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        var uri =
            request.RequestUri;

        if (
            uri is null ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !_allowedAuthorities.Contains(
                uri.Authority))
        {
            return;
        }

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                _scheme,
                _accessToken.Reveal());
    }

    public override string ToString()
    {
        return string.Concat(
            _scheme,
            " [REDACTED]");
    }
}
