// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Plans/UninstallPlan.cs
// 📌 Amac: Kurulu paketin uninstall hedefini immutable domain modeli olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Package Engine uninstall operasyonuna package kimligi, kurulu surum ve hedef root bilgisini typed olarak tasir
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Domain.Plans;

public sealed record UninstallPlan
{
    public UninstallPlan(
        PackageId packageId,
        SemanticVersion installedVersion,
        string targetPath)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(installedVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        PackageId = packageId;
        InstalledVersion = installedVersion;
        TargetPath = targetPath.Trim();
    }

    public PackageId PackageId { get; }

    public SemanticVersion InstalledVersion { get; }

    public string TargetPath { get; }
}
