// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/GitHub/GitHubReleaseProviderOptions.cs
// 📌 Amac: GitHub release provider runtime konfigurasyonunu typed model olarak tasir
// 📌 Modul - Config CSharp
// Version: 0.4.0
// Aciklama: API base URI, owner, repository, manifest asset ve user-agent degerlerini kaynak koddan ayirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Infrastructure.Providers.GitHub;

public sealed record GitHubReleaseProviderOptions
{
    public GitHubReleaseProviderOptions(
        Uri apiBaseUri,
        string owner,
        string repository,
        string manifestAssetName,
        string userAgent)
    {
        ApiBaseUri = ProviderValidation.HttpsBaseUri(apiBaseUri, nameof(apiBaseUri));
        Owner = ProviderValidation.Required(owner, nameof(owner));
        Repository = ProviderValidation.Required(repository, nameof(repository));
        ManifestAssetName = ProviderValidation.Required(manifestAssetName, nameof(manifestAssetName));
        UserAgent = ProviderValidation.Required(userAgent, nameof(userAgent));
    }

    public Uri ApiBaseUri { get; }
    public string Owner { get; }
    public string Repository { get; }
    public string ManifestAssetName { get; }
    public string UserAgent { get; }
}
