// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/UpdateCheckResult.cs
// 📌 Amac: Update check servisinin typed sonuc modelini tanimlar
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Availability, latest signed release ve mevcut installed state bilgisini tek read-only use-case sonucunda toplar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;

namespace TurkuazInstaller.Application.Updates;

public sealed record UpdateCheckResult(
    UpdateAvailability Availability,
    PackageRelease? LatestRelease,
    InstalledPackageState? InstalledState);
