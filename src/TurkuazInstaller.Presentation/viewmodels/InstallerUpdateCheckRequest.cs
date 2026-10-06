// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/viewmodels/InstallerUpdateCheckRequest.cs
// 📌 Amac: Masaustu update discovery form alanlarini runtime servisine aktarilan immutable request modelinde toplar
// 📌 Modul - ViewModel CSharp
// Version: 1.0.0
// Aciklama: Package id, release channel ve signed manifest kaynagini read-only update check requestinde tasir
//
// Bagimli Oldugu Katman: Service | View

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Presentation.ViewModels;

public sealed record InstallerUpdateCheckRequest(
    string PackageId,
    ReleaseChannel Channel,
    string ManifestSource);
