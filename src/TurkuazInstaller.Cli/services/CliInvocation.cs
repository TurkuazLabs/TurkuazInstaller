// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/services/CliInvocation.cs
// 📌 Amac: Dogrulanmis CLI operasyon istegini immutable modelde tasir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Normal mutation ve internal reboot resume isteklerini tek typed contractta toplar
//
// Bagimli Oldugu Katman: Service | Config

using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Cli.Services;

public sealed record CliInvocation(
    InstallerOperationType? Operation,
    PackageId PackageId,
    ReleaseChannel Channel,
    string? ManifestSource,
    string? RollbackManifestSource,
    string? TargetPath,
    bool Silent,
    bool IsResume);
