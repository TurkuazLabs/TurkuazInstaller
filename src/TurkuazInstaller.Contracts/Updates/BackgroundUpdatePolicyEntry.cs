// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Updates/BackgroundUpdatePolicyEntry.cs
// 📌 Amac: Background update check icin package/channel/signed manifest kaynagini typed policy entry olarak tasir
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: Session policy coordinator'a read-only update discovery hedefini immutable olarak aktarir
//
// Bagimli Oldugu Katman: Service | Config

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Updates;

public sealed record BackgroundUpdatePolicyEntry(
    PackageId PackageId,
    ReleaseChannel Channel,
    string ManifestSource);
