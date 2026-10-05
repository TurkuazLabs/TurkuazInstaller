// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerOperationProgress.cs
// 📌 Amac: Installer workflow progress bilgisini UI ve CLI katmanlarina typed olarak tasir
// 📌 Modul - Service CSharp
// Version: 0.7.1
// Aciklama: Operasyon asamasi ile 0-100 arasi progress yuzdesini dogrulanmis modelde birlestirir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Operations;

public sealed record InstallerOperationProgress
{
    public InstallerOperationProgress(
        InstallerProgressStage stage,
        int percent)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent));
        }

        Stage = stage;
        Percent = percent;
    }

    public InstallerProgressStage Stage { get; }

    public int Percent { get; }
}
