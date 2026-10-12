// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/InstallerInstallScopePolicy.cs
// 📌 Amac: Hedef kurulum motoru sistem genelinde kuruluma hazir degilken fail-closed karar verir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Ortak wizard secimlerini ayrik typed scope policy ile dogrular
//
// Bagimli Oldugu Katman: Service | Tool | Config

using TurkuazInstaller.Contracts.Operations;

namespace TurkuazInstaller.Presentation.Services;

public static class InstallerInstallScopePolicy
{
    public static void ValidateCurrentRuntime(
        InstallerInstallScope selectedScope)
    {
        if (selectedScope == InstallerInstallScope.PerUser)
        {
            return;
        }

        if (selectedScope == InstallerInstallScope.PerMachine)
        {
            throw new NotSupportedException(
                "Program Files kurulumu icin MSI, UAC ve machine-wide ownership testleri gereklidir.");
        }

        throw new ArgumentOutOfRangeException(
            nameof(selectedScope),
            selectedScope,
            "Gecersiz kurulum kapsami.");
    }
}
