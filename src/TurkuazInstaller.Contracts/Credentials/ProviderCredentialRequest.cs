// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Credentials/ProviderCredentialRequest.cs
// 📌 Amac: Provider credential resolve istegini provider turu ve HTTPS authority ile typed olarak tasir
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: Host/port authority degerini normalize ederek credential scope karisikligini ve path/query enjeksiyonunu engeller
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Credentials;

public sealed record ProviderCredentialRequest
{
    public ProviderCredentialRequest(
        ProviderCredentialProvider provider,
        string authority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            authority);

        if (
            !Uri.TryCreate(
                string.Concat(
                    Uri.UriSchemeHttps,
                    "://",
                    authority.Trim(),
                    "/"),
                UriKind.Absolute,
                out var uri) ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                uri.AbsolutePath,
                "/",
                StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(
                uri.UserInfo) ||
            !string.IsNullOrEmpty(
                uri.Query) ||
            !string.IsNullOrEmpty(
                uri.Fragment))
        {
            throw new ArgumentException(
                "Provider credential authority is invalid.",
                nameof(authority));
        }

        Provider = provider;
        Authority =
            uri.Authority
                .ToLowerInvariant();
    }

    public ProviderCredentialProvider Provider { get; }

    public string Authority { get; }
}
