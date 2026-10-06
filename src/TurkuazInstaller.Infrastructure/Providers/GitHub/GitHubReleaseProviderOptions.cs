// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/GitHub/GitHubReleaseProviderOptions.cs
// 📌 Amac: GitHub release provider runtime konfigurasyonunu typed model olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.2.0
// Aciklama: API/repo/asset ayarlarina ek olarak credential Authorization headerinin gidebilecegi HTTPS origin allow-listini sabitler
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Infrastructure.Providers.GitHub;

public sealed record GitHubReleaseProviderOptions
{
    private static readonly Uri PublicGitHubWebOrigin =
        new(
            "https://github.com/",
            UriKind.Absolute);

    public GitHubReleaseProviderOptions(
        Uri apiBaseUri,
        string owner,
        string repository,
        string manifestAssetName,
        string userAgent,
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

        UserAgent =
            ProviderValidation.Required(
                userAgent,
                nameof(userAgent));

        var origins =
            new List<Uri>();

        if (
            string.Equals(
                ApiBaseUri.Host,
                "api.github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            origins.Add(
                PublicGitHubWebOrigin);
        }

        if (credentialOrigins is not null)
        {
            origins.AddRange(
                credentialOrigins);
        }

        CredentialAuthorities =
            ProviderCredentialScope.Create(
                ToOrigin(
                    ApiBaseUri),
                origins);
    }

    public Uri ApiBaseUri { get; }

    public string Owner { get; }

    public string Repository { get; }

    public string ManifestAssetName { get; }

    public string UserAgent { get; }

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
