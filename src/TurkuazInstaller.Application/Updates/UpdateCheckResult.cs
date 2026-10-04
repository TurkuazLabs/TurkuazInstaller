// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/UpdateCheckResult.cs
// 📌 Amac: Update check servisinin typed sonuc modelini tanimlar
// 📌 Modul - Service CSharp
// Version: 0.3.0
// Aciklama: Availability ile latest release bilgisini tek use-case sonucunda toplar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Updates;

public sealed record UpdateCheckResult(UpdateAvailability Availability, PackageRelease? LatestRelease);
