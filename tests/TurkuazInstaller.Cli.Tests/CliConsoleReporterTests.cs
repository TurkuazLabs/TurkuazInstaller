// 📄 Dosya Yolu: /tests/TurkuazInstaller.Cli.Tests/CliConsoleReporterTests.cs
// 📌 Amac: CLI console reporter silent ve normal output davranisini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Silent modun stdout/stderr uretmedigini ve normal modun progress/error yazdigini dogrular
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Cli.Tools;
using Xunit;

namespace TurkuazInstaller.Cli.Tests;

public sealed class CliConsoleReporterTests
{
    [Fact]
    public void SilentReporter_DoesNotWriteStdoutOrStderr()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            Console.SetOut(output);
            Console.SetError(error);

            var reporter =
                new CliConsoleReporter(
                    silent: true);

            reporter
                .CreateProgress()
                .Report(
                    new InstallerOperationProgress(
                        InstallerProgressStage.Downloading,
                        15));

            reporter.WriteError(
                "failure");

            Assert.Equal(
                string.Empty,
                output.ToString());

            Assert.Equal(
                string.Empty,
                error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void NormalReporter_WritesProgressAndError()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            Console.SetOut(output);
            Console.SetError(error);

            var reporter =
                new CliConsoleReporter(
                    silent: false);

            reporter
                .CreateProgress()
                .Report(
                    new InstallerOperationProgress(
                        InstallerProgressStage.Verifying,
                        35));

            reporter.WriteError(
                "failure");

            Assert.Contains(
                "Verifying 35%",
                output.ToString());

            Assert.Contains(
                "failure",
                error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
