// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/PackageInstallPolicy.cs
// 📌 Amac: Manifest install target, prerequisite ve preserve path politikasini typed modelde toplar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Runtime install/update planlarinin manifestteki gercek policy verisini kullanmasini saglar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Domain.Releases;

public sealed record PackageInstallPolicy
{
    public PackageInstallPolicy(
        PackageInstallMode mode,
        string? defaultTargetPath,
        IReadOnlyList<Prerequisite> prerequisites,
        IReadOnlyList<string> preservePaths)
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
    }

    public PackageInstallMode Mode { get; }

    public string? DefaultTargetPath { get; }

    public IReadOnlyList<Prerequisite> Prerequisites { get; }

    public IReadOnlyList<string> PreservePaths { get; }

    public static PackageInstallPolicy LegacyDefault { get; } =
        new(
            PackageInstallMode.Full,
            null,
            Array.Empty<Prerequisite>(),
            Array.Empty<string>());
}
