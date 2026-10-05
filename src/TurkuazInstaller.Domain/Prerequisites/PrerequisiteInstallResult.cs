// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Prerequisites/PrerequisiteInstallResult.cs
// 📌 Amac: Prerequisite installer sonucunu exit code ve typed disposition ile Service katmanina tasir
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Reboot gereksinimini exception metni yerine typed sonuc olarak ifade eder
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Prerequisites;

public sealed record PrerequisiteInstallResult(
    PrerequisiteInstallDisposition Disposition,
    int ExitCode)
{
    public bool RequiresReboot =>
        Disposition is
            PrerequisiteInstallDisposition.RebootRequired or
            PrerequisiteInstallDisposition.RebootInitiated;

    public static PrerequisiteInstallResult Completed(
        int exitCode = 0)
    {
        return new PrerequisiteInstallResult(
            PrerequisiteInstallDisposition.Completed,
            exitCode);
    }
}
