// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/ReleaseDescriptor.cs
// 📌 Amac: Bir urunun yayinlanmis surumunu domain modeli olarak temsil eder
// 📌 Modul - Domain CSharp
// Version: 0.1.0
// Aciklama: Surum, kanal, paket, zorunluluk ve release notes bilgisini tasir
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Domain.Releases;

public sealed record ReleaseDescriptor(
    ProductId ProductId,
    Version Version,
    ReleaseChannel Channel,
    PackageDescriptor Package,
    bool Mandatory,
    string ReleaseNotes);
