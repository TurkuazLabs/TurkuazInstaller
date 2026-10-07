// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsBootstrapEnvironmentProbe.cs
// 📌 Amac: Windows runtime OS surumu ve CPU mimarisini bootstrap prerequisite portuna map eder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Windows surumu ve calisan process mimarisini typed BootstrapEnvironmentSnapshot olarak dondurur; native x64 ve ARM64 dagitimlarini ayirt eder
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
            MapArchitecture(RuntimeInformation.ProcessArchitecture));
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
