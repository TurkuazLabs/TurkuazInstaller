// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Updates/VersionUpdatePolicy.cs
// 📌 Amac: Package/channel bazli maksimum kabul edilen surum ve exact skipped surumleri typed policy modelinde tasir
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: Pinning'i maximum version ceiling olarak tanimlar; provider'da olmayan eski surumu uydurmaz
//
// Bagimli Oldugu Katman: Service | Repo | Config

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Updates;

public sealed record VersionUpdatePolicy(
    PackageId PackageId,
    ReleaseChannel Channel,
    SemanticVersion? MaximumVersion,
    IReadOnlyList<SemanticVersion> SkippedVersions);
