// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/Gitea/GiteaReleaseProviderOptions.cs
// 📌 Amac: Gitea release provider runtime konfigurasyonunu typed model olarak tasir
// 📌 Modul - Config CSharp
// Version: 0.4.0
// Aciklama: API base URI, owner, repository ve manifest asset degerlerini kaynak koddan ayirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Infrastructure.Providers.Gitea;

public sealed record GiteaReleaseProviderOptions
{
    public GiteaReleaseProviderOptions(Uri apiBaseUri, string owner, string repository, string manifestAssetName)
    {
        ApiBaseUri = ProviderValidation.HttpsBaseUri(apiBaseUri, nameof(apiBaseUri));
        Owner = ProviderValidation.Required(owner, nameof(owner));
        Repository = ProviderValidation.Required(repository, nameof(repository));
        ManifestAssetName = ProviderValidation.Required(manifestAssetName, nameof(manifestAssetName));
    }

    public Uri ApiBaseUri { get; }
    public string Owner { get; }
    public string Repository { get; }
    public string ManifestAssetName { get; }
}
