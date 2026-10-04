// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/Http/HttpReleaseProviderOptions.cs
// 📌 Amac: Generic HTTPS provider manifest adreslerini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 0.4.1
// Aciklama: Stable ve beta manifest URI degerlerini kaynak koddan ayirir ve URI'yi degistirmeden HTTPS zorunlulugu uygular
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Infrastructure.Providers.Http;

public sealed record HttpReleaseProviderOptions
{
    public HttpReleaseProviderOptions(Uri stableManifestUri, Uri betaManifestUri)
    {
        StableManifestUri = ProviderValidation.HttpsUri(stableManifestUri, nameof(stableManifestUri));
        BetaManifestUri = ProviderValidation.HttpsUri(betaManifestUri, nameof(betaManifestUri));
    }

    public Uri StableManifestUri { get; }
    public Uri BetaManifestUri { get; }
}
