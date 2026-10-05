// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Operations/InstallerEventLevel.cs
// 📌 Amac: Structured installer log severity degerlerini typed olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Information, warning ve error eventlerini logger implementationindan bagimsiz temsil eder
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Operations;

public enum InstallerEventLevel
{
    Information = 0,
    Warning = 1,
    Error = 2
}
