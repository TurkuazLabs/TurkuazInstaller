// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Credentials/ProviderCredentialProvider.cs
// 📌 Amac: Credential destekleyen release provider turlerini typed contract olarak tanimlar
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: Credential resolver ile GitHub/Gitea adapterlari arasindaki provider kimligini magic string disina tasir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Credentials;

public enum ProviderCredentialProvider
{
    GitHub = 0,
    Gitea = 1
}
