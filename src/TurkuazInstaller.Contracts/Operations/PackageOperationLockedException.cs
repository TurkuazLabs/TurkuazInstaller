// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/PackageOperationLockedException.cs
// 📌 Amac: Bir package icin baska installer islemi aktif oldugunda typed conflict hatasi tasir
// 📌 Modul - Port CSharp
// Version: 1.1.0
// Aciklama: UI ve CLI katmanlarinin lock conflict durumunu genel IO hatasindan ayirmasini saglar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Contracts.Operations;

public sealed class PackageOperationLockedException
    : InvalidOperationException
{
    public PackageOperationLockedException(
        PackageId packageId)
        : base(
            string.Concat(
                "Another installer operation is already active for package '",
                packageId.Value,
                "'."))
    {
        PackageId = packageId;
    }

    public PackageId PackageId { get; }
}
