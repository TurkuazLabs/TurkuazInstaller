// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/PackageInstallPolicy.cs
// 📌 Amac: Manifest install target, prerequisite ve preserve path politikasini typed modelde toplar
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Runtime install/update planlarinin prerequisite/preserve/windows integration policy verisini typed olarak kullanmasini saglar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Domain.Releases;

public sealed record PackageInstallPolicy
{
    public PackageInstallPolicy(
        PackageInstallMode mode,
        string? defaultTargetPath,
        IReadOnlyList<Prerequisite> prerequisites,
        IReadOnlyList<string> preservePaths,
        WindowsIntegrationPolicy? windowsIntegration = null)
    {
        ArgumentNullException.ThrowIfNull(prerequisites);
        ArgumentNullException.ThrowIfNull(preservePaths);

        Mode = mode;
        DefaultTargetPath =
            string.IsNullOrWhiteSpace(defaultTargetPath)
                ? null
                : defaultTargetPath.Trim();
        Prerequisites = prerequisites;
        PreservePaths = preservePaths;

        WindowsIntegration =
            windowsIntegration
            ?? WindowsIntegrationPolicy.Empty;
    }

    public PackageInstallMode Mode { get; }

    public string? DefaultTargetPath { get; }

    public IReadOnlyList<Prerequisite> Prerequisites { get; }

    public IReadOnlyList<string> PreservePaths { get; }

    public WindowsIntegrationPolicy WindowsIntegration
    {
        get;
    }

    public static PackageInstallPolicy LegacyDefault { get; } =
        new(
            PackageInstallMode.Full,
            null,
            Array.Empty<Prerequisite>(),
            Array.Empty<string>());
}
