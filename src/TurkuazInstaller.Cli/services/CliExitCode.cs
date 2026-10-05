// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/services/CliExitCode.cs
// 📌 Amac: CLI ve automation kullanicilari icin kararlı process exit code contractini tanimlar
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Basari, invalid invocation, reboot, cancellation ve runtime failure durumlarini typed sabitler
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Cli.Services;

public enum CliExitCode
{
    Success = 0,
    InvalidInvocation = 2,
    RebootRequired = 3,
    OperationFailed = 4,
    Cancelled = 5
}
