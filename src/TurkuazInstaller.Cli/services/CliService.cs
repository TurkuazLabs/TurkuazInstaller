// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/services/CliService.cs
// 📌 Amac: CLI request parsing, progress, terminal durum ve exit-code is kurallarini koordine eder
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Prompt kullanmadan normal/silent mutation ve reboot resume sonucunu deterministic process koduna map eder
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Cli.Config;
using TurkuazInstaller.Cli.Tools;

namespace TurkuazInstaller.Cli.Services;

public sealed class CliService
{
    private readonly CliCommandParser _parser;
    private readonly ICliInstallerRuntimeService _runtimeService;

    public CliService(
        CliCommandParser parser,
        ICliInstallerRuntimeService runtimeService)
    {
        _parser = parser;
        _runtimeService = runtimeService;
    }

    public async Task<CliExitCode> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var requestedSilent =
            arguments.Contains(
                CliArguments.SilentOption,
                StringComparer.Ordinal);

        CliInvocation invocation;

        try
        {
            invocation =
                _parser.Parse(
                    arguments);
        }
        catch (Exception exception)
            when (
                exception is FormatException or
                ArgumentException)
        {
            new CliConsoleReporter(
                    requestedSilent)
                .WriteError(
                    exception.Message);

            return CliExitCode.InvalidInvocation;
        }

        var reporter =
            new CliConsoleReporter(
                invocation.Silent);

        try
        {
            var progress =
                reporter.CreateProgress();

            if (invocation.IsResume)
            {
                await _runtimeService
                    .ResumeAsync(
                        invocation.PackageId,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _runtimeService
                    .ExecuteAsync(
                        invocation,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            return CliExitCode.Success;
        }
        catch (InstallerRebootRequiredException)
        {
            return CliExitCode.RebootRequired;
        }
        catch (OperationCanceledException)
        {
            return CliExitCode.Cancelled;
        }
        catch (Exception exception)
        {
            reporter.WriteError(
                exception.Message);

            return CliExitCode.OperationFailed;
        }
    }
}
