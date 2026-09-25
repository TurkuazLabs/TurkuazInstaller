// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/CheckForUpdateResult.cs
// 📌 Amac: Update kontrol use-case sonucunu tek nesnede tasir
// 📌 Modul - Application CSharp
// Version: 0.1.0
// Aciklama: Durum ve bulunan release bilgisini UI veya CLI katmanina iletir
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Updates;

public sealed record CheckForUpdateResult(
    UpdateAvailability Availability,
    ReleaseDescriptor? LatestRelease);
