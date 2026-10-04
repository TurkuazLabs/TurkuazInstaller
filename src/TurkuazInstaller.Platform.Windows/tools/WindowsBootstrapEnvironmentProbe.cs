// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsBootstrapEnvironmentProbe.cs
// 📌 Amac: Windows runtime OS surumu ve CPU mimarisini bootstrap prerequisite portuna map eder
// 📌 Modul - Tool CSharp
// Version: 0.6.0
// Aciklama: RuntimeInformation ve Environment API degerlerini typed BootstrapEnvironmentSnapshot olarak dondurur
//
// Bagimli Oldugu Katman: Tool

using System.Runtime.InteropServices;
using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsBootstrapEnvironmentProbe : IBootstrapEnvironmentProbe
{
    public BootstrapEnvironmentSnapshot GetSnapshot()
    {
        return new BootstrapEnvironmentSnapshot(
            OperatingSystem.IsWindows(),
            Environment.OSVersion.Version,
            MapArchitecture(RuntimeInformation.OSArchitecture));
    }

    private static BootstrapCpuArchitecture MapArchitecture(
        Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X64 => BootstrapCpuArchitecture.X64,
            Architecture.Arm64 => BootstrapCpuArchitecture.Arm64,
            _ => BootstrapCpuArchitecture.Unsupported
        };
    }
}
