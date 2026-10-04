// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Bootstrap/BootstrapPrerequisiteService.cs
// 📌 Amac: Windows bootstrap baslamadan once OS surumu ve CPU mimarisi prerequisite kurallarini uygular
// 📌 Modul - Service CSharp
// Version: 0.6.0
// Aciklama: Platform detayini IBootstrapEnvironmentProbe portundan alir ve typed prerequisite sonucu uretir
//
// Bagimli Oldugu Katman: Service | Tool | Config

using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Application.Bootstrap;

public sealed class BootstrapPrerequisiteService
{
    private readonly IBootstrapEnvironmentProbe _environmentProbe;
    private readonly BootstrapRequirements _requirements;

    public BootstrapPrerequisiteService(
        IBootstrapEnvironmentProbe environmentProbe,
        BootstrapRequirements requirements)
    {
        _environmentProbe = environmentProbe;
        _requirements = requirements;
    }

    public BootstrapPrerequisiteResult Evaluate()
    {
        var snapshot = _environmentProbe.GetSnapshot();

        if (!snapshot.IsWindows)
        {
            return BootstrapPrerequisiteResult.Failed(
                BootstrapPrerequisiteFailure.UnsupportedOperatingSystem);
        }

        if (snapshot.OperatingSystemVersion < _requirements.MinimumWindowsVersion)
        {
            return BootstrapPrerequisiteResult.Failed(
                BootstrapPrerequisiteFailure.UnsupportedWindowsVersion);
        }

        if (!_requirements.SupportedArchitectures.Contains(snapshot.Architecture))
        {
            return BootstrapPrerequisiteResult.Failed(
                BootstrapPrerequisiteFailure.UnsupportedArchitecture);
        }

        return BootstrapPrerequisiteResult.Satisfied();
    }
}
