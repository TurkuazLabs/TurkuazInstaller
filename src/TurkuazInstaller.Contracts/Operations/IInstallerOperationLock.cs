// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/IInstallerOperationLock.cs
// 📌 Amac: Ayni package icin eszamanli mutasyon islemlerini engelleyen cross-process lock portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.1.0
// Aciklama: Application workflow'unu file, mutex veya baska lock implementasyonlarindan ayirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Contracts.Operations;

public interface IInstallerOperationLock
{
    Task<IAsyncDisposable> AcquireAsync(
        PackageId packageId,
        CancellationToken cancellationToken);
}
