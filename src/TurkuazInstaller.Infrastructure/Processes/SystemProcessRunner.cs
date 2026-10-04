// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Processes/SystemProcessRunner.cs
// 📌 Amac: ProcessCommand kontratini System.Diagnostics.Process ile shell kullanmadan calistirir
// 📌 Modul - Tool CSharp
// Version: 0.5.0
// Aciklama: ArgumentList kullanarak quoting ve command injection riskini azaltir, stdout/stderr sonucunu typed olarak dondurur
//
// Bagimli Oldugu Katman: Tool

using System.Diagnostics;
using TurkuazInstaller.Contracts.System;

namespace TurkuazInstaller.Infrastructure.Processes;

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (command.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = command.WorkingDirectory;
        }

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Process could not be started.");
        }

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        var standardError = await standardErrorTask.ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }
}
