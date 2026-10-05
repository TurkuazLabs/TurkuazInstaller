// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsArchitecturePrerequisiteDetector.cs
// 📌 Amac: architecture prerequisite gereksinimini gercek Windows OS mimarisine gore dogrular
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: x64 ve arm64 architecture detection davranisini generic detector registry adapteri olarak uygular
//
// Bagimli Oldugu Katman: Tool | Service

using System.Runtime.InteropServices;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsArchitecturePrerequisiteDetector
    : IPrerequisiteDetector
{
    private const string X64Architecture = "x64";
    private const string Arm64Architecture = "arm64";

    public string Id =>
        PrerequisiteIds.Architecture;

    public Task<bool> IsSatisfiedAsync(
        Prerequisite prerequisite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prerequisite);
        cancellationToken.ThrowIfCancellationRequested();

        var expected =
            prerequisite.VersionExpression
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

        return Task.FromResult(
            string.Equals(
                current,
                expected,
                StringComparison.Ordinal));
    }
}
