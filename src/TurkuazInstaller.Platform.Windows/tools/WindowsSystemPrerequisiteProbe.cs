// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsSystemPrerequisiteProbe.cs
// 📌 Amac: Windows build, mimari ve .NET Desktop Runtime prerequisite gereksinimlerini gercek sistemde dogrular
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Manifest prerequisite portunu Windows ortam bilgisi ve runtime klasorleri uzerinden fail-closed uygular
//
// Bagimli Oldugu Katman: Tool | Service

using System.Runtime.InteropServices;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsSystemPrerequisiteProbe
    : ISystemPrerequisiteProbe
{
    private const string GreaterThanOrEqual = ">=";
    private const string LessThanOrEqual = "<=";
    private const string GreaterThan = ">";
    private const string LessThan = "<";
    private const string Equal = "=";

    private const string X64Architecture = "x64";
    private const string Arm64Architecture = "arm64";

    private const string DotNetDirectory = "dotnet";
    private const string SharedDirectory = "shared";
    private const string WindowsDesktopRuntimeDirectory =
        "Microsoft.WindowsDesktop.App";

    public Task<bool> IsSatisfiedAsync(
        Prerequisite prerequisite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prerequisite);
        cancellationToken.ThrowIfCancellationRequested();

        var result =
            prerequisite.Id switch
            {
                PrerequisiteIds.WindowsBuild =>
                    IsWindowsBuildSatisfied(
                        prerequisite.VersionExpression),
                PrerequisiteIds.Architecture =>
                    IsArchitectureSatisfied(
                        prerequisite.VersionExpression),
                PrerequisiteIds.DotNetDesktopRuntime =>
                    IsDesktopRuntimeSatisfied(
                        prerequisite.VersionExpression),
                _ => false
            };

        return Task.FromResult(result);
    }

    private static bool IsWindowsBuildSatisfied(
        string expression)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return TryMatchInteger(
            Environment.OSVersion.Version.Build,
            expression);
    }

    private static bool IsArchitectureSatisfied(
        string expression)
    {
        var expected =
            expression
                .Trim()
                .ToLowerInvariant();

        var current =
            RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 =>
                    X64Architecture,
                Architecture.Arm64 =>
                    Arm64Architecture,
                _ =>
                    string.Empty
            };

        return string.Equals(
            current,
            expected,
            StringComparison.Ordinal);
    }

    private static bool IsDesktopRuntimeSatisfied(
        string expression)
    {
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
                var name =
                    Path.GetFileName(
                        directory);

                if (
                    Version.TryParse(
                        name,
                        out var version) &&
                    TryMatchVersion(
                        version,
                        expression))
                {
                    return true;
                }
            }
        }

        return false;
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

    private static bool TryMatchInteger(
        int current,
        string expression)
    {
        if (
            !TryParseExpression(
                expression,
                out var operation,
                out var valueText) ||
            !int.TryParse(
                valueText,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var expected))
        {
            return false;
        }

        var comparison =
            current.CompareTo(expected);

        return EvaluateComparison(
            comparison,
            operation);
    }

    private static bool TryMatchVersion(
        Version current,
        string expression)
    {
        if (
            !TryParseExpression(
                expression,
                out var operation,
                out var valueText) ||
            !Version.TryParse(
                valueText,
                out var expected))
        {
            return false;
        }

        var comparison =
            current.CompareTo(expected);

        return EvaluateComparison(
            comparison,
            operation);
    }

    private static bool TryParseExpression(
        string expression,
        out string operation,
        out string value)
    {
        var trimmed =
            expression.Trim();

        foreach (
            var candidate in
            new[]
            {
                GreaterThanOrEqual,
                LessThanOrEqual,
                GreaterThan,
                LessThan,
                Equal
            })
        {
            if (
                trimmed.StartsWith(
                    candidate,
                    StringComparison.Ordinal))
            {
                operation = candidate;
                value =
                    trimmed[candidate.Length..]
                        .Trim();

                return value.Length > 0;
            }
        }

        operation = Equal;
        value = trimmed;
        return value.Length > 0;
    }

    private static bool EvaluateComparison(
        int comparison,
        string operation)
    {
        return operation switch
        {
            GreaterThanOrEqual =>
                comparison >= 0,
            LessThanOrEqual =>
                comparison <= 0,
            GreaterThan =>
                comparison > 0,
            LessThan =>
                comparison < 0,
            Equal =>
                comparison == 0,
            _ =>
                false
        };
    }
}
