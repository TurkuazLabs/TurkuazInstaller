// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsElevatedProcessRunner.cs
// 📌 Amac: Yalniz explicit privileged isteklerde Windows UAC runas verb'i ile child process calistirir
// 📌 Modul - Tool CSharp
// Version: 0.6.0
// Aciklama: Normal IProcessRunner akisini unelevated tutar ve UAC iptalini typed ElevationResult olarak dondurur
//
// Bagimli Oldugu Katman: Tool

using System.ComponentModel;
using System.Diagnostics;
using TurkuazInstaller.Contracts.System;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsElevatedProcessRunner : IElevatedProcessRunner
{
    private const int WindowsErrorCancelled = 1223;
    private const string RunAsVerb = "runas";

    public async Task<ElevationResult> RunElevatedAsync(
        ElevationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows UAC elevation is only available on Windows.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.ExecutableDirectory,
            UseShellExecute = true,
            Verb = RunAsVerb
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    "Elevated process could not be started.");

            await process
                .WaitForExitAsync(cancellationToken)
                .ConfigureAwait(false);

            return new ElevationResult(
                ElevationStatus.Completed,
                process.ExitCode);
        }
        catch (Win32Exception exception)
            when (exception.NativeErrorCode == WindowsErrorCancelled)
        {
            return new ElevationResult(
                ElevationStatus.Cancelled,
                null);
        }
    }
}
