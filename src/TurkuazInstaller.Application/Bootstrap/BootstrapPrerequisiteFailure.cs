// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Bootstrap/BootstrapPrerequisiteFailure.cs
// 📌 Amac: Bootstrap prerequisite reddetme nedenlerini typed enum olarak tanimlar
// 📌 Modul - Service CSharp
// Version: 0.6.0
// Aciklama: OS, Windows surumu ve CPU mimarisi hatalarini magic string kullanmadan ayirir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Bootstrap;

public enum BootstrapPrerequisiteFailure
{
    None = 0,
    UnsupportedOperatingSystem = 1,
    UnsupportedWindowsVersion = 2,
    UnsupportedArchitecture = 3
}
