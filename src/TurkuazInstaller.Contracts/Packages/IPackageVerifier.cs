// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/IPackageVerifier.cs
// 📌 Amac: Paket guvenlik dogrulamasi icin provider bagimsiz portu tanimlar
// 📌 Modul - Contract CSharp
// Version: 0.1.0
// Aciklama: Hash ve Authenticode kontrollerinin Application tarafindan cagirilmasini saglar
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Packages;

public interface IPackageVerifier
{
    Task<bool> VerifyAsync(
        PackageDescriptor package,
        string localPath,
        CancellationToken cancellationToken);
}
