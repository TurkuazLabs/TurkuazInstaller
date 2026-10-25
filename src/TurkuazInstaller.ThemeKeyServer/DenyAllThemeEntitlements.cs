// Dosya Yolu: src/TurkuazInstaller.ThemeKeyServer/DenyAllThemeEntitlements.cs
// Amac: Varsayilan serverda hicbir kullanicinin anahtar cikaramamasi
// Modul - CSharp Server Adapters
// Version: 0.1.0-qa
// Aciklama: KMS ve entitlement servisleri gercek provider ile degistirilmeden deny
// Bagimli Oldugu Katman: Infrastructure | Security

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Branding;

namespace TurkuazInstaller.ThemeKeyServer;

internal sealed class DenyAllThemeEntitlements : IThemeKeyEntitlementStore
{
    public Task<bool> IsAuthorizedAsync(
        string authenticatedSubject, PackageId packageId, string keyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }
}

internal sealed class UnavailableThemeKeyVault : IThemeKeySecretStore
{
    public ValueTask<byte[]?> RetrieveOwnedKeyAsync(
        PackageId packageId, string keyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<byte[]?>(null);
    }
}
