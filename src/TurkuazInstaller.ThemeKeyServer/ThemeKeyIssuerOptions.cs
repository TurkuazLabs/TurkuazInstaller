// Dosya Yolu: src/TurkuazInstaller.ThemeKeyServer/ThemeKeyIssuerOptions.cs
// Amac: OIDC yetki sunucusu ayarlarini host startup asamasinda fail-closed denetlemek
// Modul - CSharp Server Config
// Version: 0.1.0-qa
// Aciklama: TestServer configuration da normal production configuration da ayni kural
// Bagimli Oldugu Katman: Microsoft.Extensions.Options | Security

namespace TurkuazInstaller.ThemeKeyServer;

public sealed class ThemeKeyIssuerOptions
{
    public string? Authority { get; set; }
    public string? Audience { get; set; }

    public bool IsValid()
    {
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var authority) ||
            authority.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(authority.Host) ||
            !string.IsNullOrEmpty(authority.UserInfo) ||
            !string.IsNullOrEmpty(authority.Query) ||
            !string.IsNullOrEmpty(authority.Fragment) ||
            string.IsNullOrWhiteSpace(Audience) ||
            Audience.Length > 256)
        {
            return false;
        }
        return true;
    }

    public string GetTrustedIssuer()
    {
        if (!IsValid()) throw new InvalidOperationException(
            "Trusted ThemeKeyIssuer HTTPS Authority and Audience must be configured.");
        return Authority!.TrimEnd('/');
    }
}
