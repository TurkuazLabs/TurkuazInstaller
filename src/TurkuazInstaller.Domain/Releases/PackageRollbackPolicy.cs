// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/PackageRollbackPolicy.cs
// 📌 Amac: Manifest rollback destegini typed domain policy olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Runtime rollback isleminin urun manifest politikasina gore izinli olup olmadigini tasir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Releases;

public sealed record PackageRollbackPolicy(
    bool Supported,
    bool PreviousVersionRequired)
{
    public static PackageRollbackPolicy Disabled { get; } =
        new(
            Supported: false,
            PreviousVersionRequired: false);
}
