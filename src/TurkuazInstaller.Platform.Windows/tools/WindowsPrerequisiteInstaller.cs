// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsPrerequisiteInstaller.cs
// 📌 Amac: Dogrulanmis prerequisite installer executable dosyasini normal veya explicit UAC elevation ile calistirir
// 📌 Modul - Tool CSharp
// Version: 1.1.1
// Aciklama: Yalniz dogrudan EXE calistirir, shell-free argument listesi kullanir ve reboot-required sonucunu fail-closed dondurur
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsPrerequisiteInstaller
    : IPrerequisiteInstaller
{
    private const int SuccessExitCode = 0;
    private const int RebootInitiatedExitCode = 1641;
    private const int RebootRequiredExitCode = 3010;

    private readonly IProcessRunner _processRunner;
    private readonly IElevatedProcessRunner _elevatedProcessRunner;

    public WindowsPrerequisiteInstaller(
        IProcessRunner processRunner,
        IElevatedProcessRunner elevatedProcessRunner)
    {
        _processRunner = processRunner;
        _elevatedProcessRunner = elevatedProcessRunner;
    }

    public async Task InstallAsync(
        string verifiedInstallerPath,
        PrerequisiteInstallAction installAction,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            verifiedInstallerPath);
        ArgumentNullException.ThrowIfNull(
            installAction);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows prerequisite installer is only available on Windows.");
        }

        var fullPath =
            Path.GetFullPath(
                verifiedInstallerPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Verified prerequisite installer file was not found.",
                fullPath);
        }

        if (
            !string.Equals(
                Path.GetExtension(
                    fullPath),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Prerequisite auto-install only supports direct EXE artifacts.");
        }

        var workingDirectory =
            Path.GetDirectoryName(
                fullPath)
            ?? throw new InvalidOperationException(
                "Prerequisite installer working directory could not be resolved.");

        if (installAction.RequiresElevation)
        {
            var elevatedResult =
                await _elevatedProcessRunner
                    .RunElevatedAsync(
                        new ElevationRequest(
                            fullPath,
                            installAction.Arguments,
                            workingDirectory),
                        cancellationToken)
                    .ConfigureAwait(false);

            if (
                elevatedResult.Status ==
                ElevationStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "Prerequisite installer elevation was cancelled.");
            }

            ValidateExitCode(
                elevatedResult.ExitCode);
            return;
        }

        var result =
            await _processRunner
                .RunAsync(
                    new ProcessCommand(
                        fullPath,
                        installAction.Arguments,
                        workingDirectory),
                    cancellationToken)
                .ConfigureAwait(false);

        ValidateExitCode(
            result.ExitCode);
    }

    private static void ValidateExitCode(
        int? exitCode)
    {
        if (exitCode is null)
        {
            throw new InvalidOperationException(
                "Prerequisite installer did not return an exit code.");
        }

        if (
            exitCode is RebootInitiatedExitCode or
            RebootRequiredExitCode)
        {
            throw new InvalidOperationException(
                "Prerequisite installer requires a reboot; reboot/resume orchestration is not available yet.");
        }

        if (exitCode != SuccessExitCode)
        {
            throw new InvalidOperationException(
                string.Concat(
                    "Prerequisite installer failed with exit code ",
                    exitCode.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    "."));
        }
    }
}
