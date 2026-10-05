// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/viewmodels/InstallerDesktopRequest.cs
// 📌 Amac: ViewModel alanlarini installer runtime servisine aktarilan immutable request modelinde toplar
// 📌 Modul - ViewModel CSharp
// Version: 0.7.0
// Aciklama: Operation, package, channel, manifest ve target degerlerini tek requestte tasir
//
// Bagimli Oldugu Katman: Service | View

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Presentation.ViewModels;

public sealed record InstallerDesktopRequest(
    InstallerOperationKind Operation,
    string PackageId,
    ReleaseChannel Channel,
    string ManifestSource,
    string? RollbackManifestSource,
    string TargetPath);
