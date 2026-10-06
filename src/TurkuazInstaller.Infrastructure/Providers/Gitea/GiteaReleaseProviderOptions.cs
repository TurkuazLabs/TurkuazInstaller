// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/Gitea/GiteaReleaseProviderOptions.cs
// 📌 Amac: Gitea release provider runtime konfigurasyonunu typed model olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.2.0
// Aciklama: API/repo/asset ayarlarina ek olarak token headerinin gidebilecegi HTTPS origin allow-listini sabitler
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Infrastructure.Providers.Gitea;

public sealed record GiteaReleaseProviderOptions
{
    public GiteaReleaseProviderOptions(
        Uri apiBaseUri,
        string owner,
        string repository,
        string manifestAssetName,
        IReadOnlyList<Uri>? credentialOrigins = null)
    {
        ApiBaseUri =
            ProviderValidation.HttpsBaseUri(
                apiBaseUri,
                nameof(apiBaseUri));

        Owner =
            ProviderValidation.Required(
                owner,
                nameof(owner));

        Repository =
            ProviderValidation.Required(
                repository,
                nameof(repository));

        ManifestAssetName =
            ProviderValidation.Required(
                manifestAssetName,
                nameof(manifestAssetName));

        CredentialAuthorities =
            ProviderCredentialScope.Create(
                ToOrigin(
                    ApiBaseUri),
                credentialOrigins);
    }

    public Uri ApiBaseUri { get; }

    public string Owner { get; }

    public string Repository { get; }

    public string ManifestAssetName { get; }

    internal IReadOnlySet<string> CredentialAuthorities
    {
        get;
    }

    private static Uri ToOrigin(
        Uri uri)
    {
        return new Uri(
            string.Concat(
                uri.Scheme,
                "://",
                uri.Authority,
                "/"),
            UriKind.Absolute);
    }
}
