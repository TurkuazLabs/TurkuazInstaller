// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/BootstrapPrerequisiteServiceTests.cs
// 📌 Amac: Bootstrap prerequisite servisinin OS, surum ve architecture kararlarini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 0.6.0
// Aciklama: Windows 10 1809 tabani ile x64 ve arm64 kabul kurallarini fake environment probe kullanarak test eder
//
// Bagimli Oldugu Katman: Service | Tool | Config

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Contracts.Bootstrap;
using Xunit;

namespace TurkuazInstaller.Application.Tests;

public sealed class BootstrapPrerequisiteServiceTests
{
    private static readonly BootstrapRequirements Requirements = new(
        new Version(10, 0, 17763, 0),
        new[]
        {
            BootstrapCpuArchitecture.X64,
            BootstrapCpuArchitecture.Arm64
        });

    [Fact]
    public void Evaluate_SupportedWindowsX64_ReturnsSatisfied()
    {
        var service = CreateService(
            true,
            new Version(10, 0, 19045, 0),
            BootstrapCpuArchitecture.X64);

        var result = service.Evaluate();

        Assert.True(result.IsSatisfied);
        Assert.Equal(
            BootstrapPrerequisiteFailure.None,
            result.Failure);
    }

    [Fact]
    public void Evaluate_OldWindows_ReturnsVersionFailure()
    {
        var service = CreateService(
            true,
            new Version(10, 0, 16299, 0),
            BootstrapCpuArchitecture.X64);

        var result = service.Evaluate();

        Assert.False(result.IsSatisfied);
        Assert.Equal(
            BootstrapPrerequisiteFailure.UnsupportedWindowsVersion,
            result.Failure);
    }

    [Fact]
    public void Evaluate_NonWindows_ReturnsOperatingSystemFailure()
    {
        var service = CreateService(
            false,
            new Version(0, 0),
            BootstrapCpuArchitecture.X64);

        var result = service.Evaluate();

        Assert.Equal(
            BootstrapPrerequisiteFailure.UnsupportedOperatingSystem,
            result.Failure);
    }

    [Fact]
    public void Evaluate_UnsupportedArchitecture_ReturnsArchitectureFailure()
    {
        var service = CreateService(
            true,
            new Version(10, 0, 26100, 0),
            BootstrapCpuArchitecture.Unsupported);

        var result = service.Evaluate();

        Assert.Equal(
            BootstrapPrerequisiteFailure.UnsupportedArchitecture,
            result.Failure);
    }

    private static BootstrapPrerequisiteService CreateService(
        bool isWindows,
        Version version,
        BootstrapCpuArchitecture architecture)
    {
        return new BootstrapPrerequisiteService(
            new StubEnvironmentProbe(
                new BootstrapEnvironmentSnapshot(
                    isWindows,
                    version,
                    architecture)),
            Requirements);
    }

    private sealed class StubEnvironmentProbe : IBootstrapEnvironmentProbe
    {
        private readonly BootstrapEnvironmentSnapshot _snapshot;

        public StubEnvironmentProbe(
            BootstrapEnvironmentSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public BootstrapEnvironmentSnapshot GetSnapshot() =>
            _snapshot;
    }
}
