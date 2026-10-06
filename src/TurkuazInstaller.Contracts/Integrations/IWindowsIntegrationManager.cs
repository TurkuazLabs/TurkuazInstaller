// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Integrations/IWindowsIntegrationManager.cs
// 📌 Amac: Application workflow ile Windows shortcut/protocol adapteri arasindaki package-scoped portu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Signed typed policy reconcile ve owned integration cleanup davranislarini Windows detaylarindan ayirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Contracts.Integrations;

public interface IWindowsIntegrationManager
{
    Task ApplyAsync(
        PackageId packageId,
        string targetPath,
        WindowsIntegrationPolicy policy,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        PackageId packageId,
        CancellationToken cancellationToken);
}
