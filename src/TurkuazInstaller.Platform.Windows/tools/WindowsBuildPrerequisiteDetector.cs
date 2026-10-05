// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsBuildPrerequisiteDetector.cs
// 📌 Amac: windows-build prerequisite gereksinimini gercek Windows build numarasina gore dogrular
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Generic detector registry icin Windows build adapterini saglar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsBuildPrerequisiteDetector
    : IPrerequisiteDetector
{
    public string Id =>
        PrerequisiteIds.WindowsBuild;

    public Task<bool> IsSatisfiedAsync(
        Prerequisite prerequisite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prerequisite);
        cancellationToken.ThrowIfCancellationRequested();

        var satisfied =
            OperatingSystem.IsWindows() &&
            PrerequisiteExpression.Matches(
                Environment.OSVersion.Version.Build,
                prerequisite.VersionExpression);

        return Task.FromResult(satisfied);
    }
}
