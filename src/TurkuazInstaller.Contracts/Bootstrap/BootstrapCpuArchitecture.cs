// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/BootstrapCpuArchitecture.cs
// 📌 Amac: Bootstrap tarafindan desteklenen Windows CPU mimarilerini typed olarak tanimlar
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: X64, Arm64 ve desteklenmeyen mimari durumlarini magic string kullanmadan temsil eder
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public enum BootstrapCpuArchitecture
{
    Unsupported = 0,
    X64 = 1,
    Arm64 = 2
}
