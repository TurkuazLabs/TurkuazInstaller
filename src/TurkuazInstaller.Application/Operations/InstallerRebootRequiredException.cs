// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerRebootRequiredException.cs
// 📌 Amac: Guvenli prerequisite kurulumundan sonra reboot bekleyen workflow sinirini typed olarak bildirir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: AwaitingReboot journal checkpointini normal failure durumundan ayirir
//
// Bagimli Oldugu Katman: Service | Tool

using System.Globalization;
using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Application.Operations;

public sealed class InstallerRebootRequiredException
    : InvalidOperationException
{
    public InstallerRebootRequiredException(
        string prerequisiteId,
        PrerequisiteInstallDisposition disposition,
        int exitCode)
        : base(
            string.Concat(
                "Prerequisite installation requires reboot before the installer workflow can resume: ",
                prerequisiteId,
                " (",
                disposition.ToString(),
                ", exit code ",
                exitCode.ToString(
                    CultureInfo.InvariantCulture),
                ")."))
    {
        PrerequisiteId = prerequisiteId;
        Disposition = disposition;
        ExitCode = exitCode;
    }

    public string PrerequisiteId { get; }

    public PrerequisiteInstallDisposition Disposition { get; }

    public int ExitCode { get; }
}
