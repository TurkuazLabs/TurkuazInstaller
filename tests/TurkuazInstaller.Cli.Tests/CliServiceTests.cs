// 📄 Dosya Yolu: /tests/TurkuazInstaller.Cli.Tests/CliServiceTests.cs
// 📌 Amac: CLI Service terminal durumlarini deterministic process exit kodlarina map etmeyi test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Success, invalid invocation, reboot required ve runtime failure exit-code davranislarini kapsar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Cli.Services;
using TurkuazInstaller.Cli.Tools;
using TurkuazInstaller.Domain.Prerequisites;
using TurkuazInstaller.Domain.Products;
using Xunit;

namespace TurkuazInstaller.Cli.Tests;

public sealed class CliServiceTests
{
    [Fact]
    public async Task RunAsync_ValidUninstall_ReturnsSuccess()
    {
        var runtime =
            new StubRuntimeService();

        var service =
            new CliService(
                new CliCommandParser(),
                runtime);

        var result =
            await service.RunAsync(
                new[]
                {
                    "uninstall",
                    "--package",
                    "example-app",
                    "--silent"
                },
                CancellationToken.None);

        Assert.Equal(
            CliExitCode.Success,
            result);

        Assert.NotNull(
            runtime.Invocation);
    }

    [Fact]
    public async Task RunAsync_InvalidInvocation_ReturnsInvalidInvocation()
    {
        var service =
            new CliService(
                new CliCommandParser(),
                new StubRuntimeService());

        var result =
            await service.RunAsync(
                Array.Empty<string>(),
                CancellationToken.None);

        Assert.Equal(
            CliExitCode.InvalidInvocation,
            result);
    }

    [Fact]
    public async Task RunAsync_RebootRequired_ReturnsRebootExitCode()
    {
        var runtime =
            new StubRuntimeService
            {
                Exception =
                    new InstallerRebootRequiredException(
                        "dotnet-desktop-runtime",
                        PrerequisiteInstallDisposition.RebootRequired,
                        3010)
            };

        var service =
            new CliService(
                new CliCommandParser(),
                runtime);

        var result =
            await service.RunAsync(
                new[]
                {
                    "uninstall",
                    "--package",
                    "example-app",
                    "--silent"
                },
                CancellationToken.None);

        Assert.Equal(
            CliExitCode.RebootRequired,
            result);
    }

    [Fact]
    public async Task RunAsync_InternalResume_InvokesResumePath()
    {
        var runtime =
            new StubRuntimeService();

        var service =
            new CliService(
                new CliCommandParser(),
                runtime);

        var result =
            await service.RunAsync(
                new[]
                {
                    "--resume-package",
                    "example-app",
                    "--silent"
                },
                CancellationToken.None);

        Assert.Equal(
            CliExitCode.Success,
            result);

        Assert.Equal(
            "example-app",
            runtime.ResumePackageId?.Value);
    }

    private sealed class StubRuntimeService
        : ICliInstallerRuntimeService
    {
        public Exception? Exception { get; init; }

        public CliInvocation? Invocation { get; private set; }

        public PackageId? ResumePackageId { get; private set; }

        public Task ExecuteAsync(
            CliInvocation invocation,
            IProgress<InstallerOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            Invocation = invocation;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.CompletedTask;
        }

        public Task ResumeAsync(
            PackageId packageId,
            IProgress<InstallerOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            ResumePackageId = packageId;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.CompletedTask;
        }
    }
}
