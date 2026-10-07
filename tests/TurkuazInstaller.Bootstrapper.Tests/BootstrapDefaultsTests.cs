// 📄 Dosya Yolu: /tests/TurkuazInstaller.Bootstrapper.Tests/BootstrapDefaultsTests.cs
// 📌 Amac: Native x64 ve ARM64 bootstrap runtime varsayilanlarini architecture-safe olarak dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.1
// Aciklama: Supported architecture listesi ile x64 ve ARM64 self-update asset adlarini unit test ile sabitler
//
// Bagimli Oldugu Katman: Config | Tool

using TurkuazInstaller.Bootstrapper.Config;
using TurkuazInstaller.Contracts.Bootstrap;
using Xunit;

namespace TurkuazInstaller.Bootstrapper.Tests;

public sealed class BootstrapDefaultsTests
{
    [Fact]
    public void CreateRequirements_SupportsX64AndArm64()
    {
        var requirements =
            BootstrapDefaults.CreateRequirements();

        Assert.Contains(
            BootstrapCpuArchitecture.X64,
            requirements.SupportedArchitectures);

        Assert.Contains(
            BootstrapCpuArchitecture.Arm64,
            requirements.SupportedArchitectures);
    }

    [Theory]
    [InlineData(
        BootstrapCpuArchitecture.X64,
        "TurkuazInstaller.Bootstrapper.exe")]
    [InlineData(
        BootstrapCpuArchitecture.Arm64,
        "TurkuazInstaller.Bootstrapper-win-arm64.exe")]
    public void CreateSelfUpdateDiscoveryOptions_UsesArchitectureSpecificAsset(
        BootstrapCpuArchitecture architecture,
        string expectedAssetName)
    {
        var options =
            BootstrapDefaults
                .CreateSelfUpdateDiscoveryOptions(
                    architecture);

        Assert.Equal(
            expectedAssetName,
            options.AssetName);
    }

    [Fact]
    public void CreateSelfUpdateDiscoveryOptions_UnsupportedArchitecture_UsesNonExecutingFallback()
    {
        var options =
            BootstrapDefaults
                .CreateSelfUpdateDiscoveryOptions(
                    BootstrapCpuArchitecture.Unsupported);

        Assert.Equal(
            "TurkuazInstaller.Bootstrapper.exe",
            options.AssetName);
    }
}
