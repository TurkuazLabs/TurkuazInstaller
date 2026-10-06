// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Credentials/IProviderCredentialResolver.cs
// 📌 Amac: Private provider access tokenini manifest/config sinirindan ayri secret store adapterindan resolve eden portu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Credential yoksa null dondurerek public provider davranisini korur
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Credentials;

public interface IProviderCredentialResolver
{
    Task<ProviderAccessToken?> ResolveAsync(
        ProviderCredentialRequest request,
        CancellationToken cancellationToken);
}
