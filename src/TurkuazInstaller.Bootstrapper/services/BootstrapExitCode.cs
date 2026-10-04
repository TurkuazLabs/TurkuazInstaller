// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/services/BootstrapExitCode.cs
// 📌 Amac: Native bootstrap process exit kodlarini typed enum olarak tanimlar
// 📌 Modul - Service CSharp
// Version: 0.6.0
// Aciklama: Success ve prerequisite failure durumlarini magic number kullanmadan process sonucuna map eder
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Bootstrapper.Services;

internal enum BootstrapExitCode
{
    Success = 0,
    UnsupportedOperatingSystem = 10,
    UnsupportedWindowsVersion = 11,
    UnsupportedArchitecture = 12,
    InvalidInvocation = 20,
    RuntimeFailure = 21
}
