// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsDesktopRuntimePrerequisiteDetector.cs
// 📌 Amac: dotnet-desktop-runtime prerequisite gereksinimini kurulu Windows Desktop Runtime klasorlerinden dogrular
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Kurulu Microsoft.WindowsDesktop.App surumlerini generic detector registry icin fail-closed tarar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsDesktopRuntimePrerequisiteDetector
    : IPrerequisiteDetector
{
    private const string DotNetDirectory = "dotnet";
    private const string SharedDirectory = "shared";
    private const string WindowsDesktopRuntimeDirectory =
        "Microsoft.WindowsDesktop.App";

    public string Id =>
        PrerequisiteIds.DotNetDesktopRuntime;

    public Task<bool> IsSatisfiedAsync(
        Prerequisite prerequisite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prerequisite);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (
            var root in
            GetDotNetRoots())
        {
            var sharedPath =
                Path.Combine(
                    root,
                    SharedDirectory,
                    WindowsDesktopRuntimeDirectory);

            if (!Directory.Exists(sharedPath))
            {
                continue;
            }

            foreach (
                var directory in
                Directory.EnumerateDirectories(
                    sharedPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var name =
                    Path.GetFileName(
                        directory);

                if (
                    Version.TryParse(
                        name,
                        out var version) &&
                    PrerequisiteExpression.Matches(
                        version,
                        prerequisite.VersionExpression))
                {
                    return Task.FromResult(true);
                }
            }
        }

        return Task.FromResult(false);
    }

    private static IEnumerable<string> GetDotNetRoots()
    {
        var programFiles =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles);

        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(
                programFiles,
                DotNetDirectory);
        }

        var programFilesX86 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);

        if (
            !string.IsNullOrWhiteSpace(
                programFilesX86) &&
            !string.Equals(
                programFiles,
                programFilesX86,
                StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(
                programFilesX86,
                DotNetDirectory);
        }
    }
}
