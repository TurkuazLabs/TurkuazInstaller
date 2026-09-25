// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/CpuArchitecture.cs
// 📌 Amac: Paket CPU mimarisini type-safe olarak temsil eder
// 📌 Modul - Domain CSharp
// Version: 0.1.0
// Aciklama: x64 ve ARM64 paket secimini string bagimliligindan kurtarir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Releases;

public enum CpuArchitecture
{
    X64 = 0,
    Arm64 = 1
}
