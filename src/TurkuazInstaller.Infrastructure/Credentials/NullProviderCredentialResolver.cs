// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Credentials/NullProviderCredentialResolver.cs
// 📌 Amac: Credential configure edilmediginde provider public davranisini koruyan null-object resolver saglar
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: GitHub/Gitea provider overloadlarinin mevcut public kullanimini geriye uyumlu tutar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Credentials;

namespace TurkuazInstaller.Infrastructure.Credentials;

internal sealed class NullProviderCredentialResolver
    : IProviderCredentialResolver
{
    public static NullProviderCredentialResolver Instance
    {
        get;
    } = new();

    private NullProviderCredentialResolver()
    {
    }

    public Task<ProviderAccessToken?> ResolveAsync(
        ProviderCredentialRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<ProviderAccessToken?>(
            null);
    }
}
