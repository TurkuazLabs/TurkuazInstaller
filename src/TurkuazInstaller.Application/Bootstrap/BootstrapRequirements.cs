// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Bootstrap/BootstrapRequirements.cs
// 📌 Amac: Bootstrap prerequisite politikasini inline config kullanmadan typed Application konfigurasyonu olarak tasir
// 📌 Modul - Config CSharp
// Version: 0.6.0
// Aciklama: Minimum Windows surumu ve desteklenen CPU mimarilerini prerequisite servisine aktarir
//
// Bagimli Oldugu Katman: Service | Config

using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Application.Bootstrap;

public sealed record BootstrapRequirements
{
    public BootstrapRequirements(
        Version minimumWindowsVersion,
        IReadOnlyCollection<BootstrapCpuArchitecture> supportedArchitectures)
    {
        ArgumentNullException.ThrowIfNull(minimumWindowsVersion);
        ArgumentNullException.ThrowIfNull(supportedArchitectures);

        if (supportedArchitectures.Count == 0)
        {
            throw new ArgumentException(
                "At least one supported architecture is required.",
                nameof(supportedArchitectures));
        }

        MinimumWindowsVersion = minimumWindowsVersion;
        SupportedArchitectures = Array.AsReadOnly(
            supportedArchitectures.Distinct().ToArray());
    }

    public Version MinimumWindowsVersion { get; }

    public IReadOnlyCollection<BootstrapCpuArchitecture> SupportedArchitectures { get; }
}
