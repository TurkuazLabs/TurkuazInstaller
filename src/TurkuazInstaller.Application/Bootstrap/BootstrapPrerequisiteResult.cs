// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Bootstrap/BootstrapPrerequisiteResult.cs
// 📌 Amac: Bootstrap prerequisite kontrol sonucunu typed model olarak tasir
// 📌 Modul - Service CSharp
// Version: 0.6.0
// Aciklama: Success ve failure reason bilgisini UI oncesi runtime katmanina aktarir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Bootstrap;

public sealed record BootstrapPrerequisiteResult(
    bool IsSatisfied,
    BootstrapPrerequisiteFailure Failure)
{
    public static BootstrapPrerequisiteResult Satisfied() =>
        new(true, BootstrapPrerequisiteFailure.None);

    public static BootstrapPrerequisiteResult Failed(
        BootstrapPrerequisiteFailure failure)
    {
        if (failure == BootstrapPrerequisiteFailure.None)
        {
            throw new ArgumentException(
                "Failure reason is required.",
                nameof(failure));
        }

        return new BootstrapPrerequisiteResult(
            false,
            failure);
    }
}
