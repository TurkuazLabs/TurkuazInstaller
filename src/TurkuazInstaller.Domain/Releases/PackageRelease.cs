// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/PackageRelease.cs
// 📌 Amac: Provider tarafindan bulunan paket release bilgisini typed domain modeli olarak tasir
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Paket, surum, kanal ve artifact metadata'sini provider bagimsiz birlestirir
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Domain.Releases;

public sealed record PackageRelease(
    PackageId PackageId,
    SemanticVersion Version,
    ReleaseChannel Channel,
    ArtifactDescriptor Artifact);
