// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/PackageEngineOperation.cs
// 📌 Amac: Package Engine hata ve log olaylarinda operasyon turunu typed olarak tanimlar
// 📌 Modul - Port CSharp
// Version: 0.5.0
// Aciklama: Stage, apply, repair ve rollback operasyonlarini magic string olmadan temsil eder
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Packages;

public enum PackageEngineOperation
{
    Stage = 0,
    Apply = 1,
    Repair = 2,
    Rollback = 3
}
