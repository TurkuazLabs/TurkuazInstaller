// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/PackageDescriptor.cs
// 📌 Amac: Indirilecek release paketinin degismez domain tanimini tasir
// 📌 Modul - Domain CSharp
// Version: 0.1.0
// Aciklama: URL, mimari, boyut ve SHA-256 bilgisini tek modelde toplar
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Releases;

public sealed record PackageDescriptor(
    Uri DownloadUri,
    CpuArchitecture Architecture,
    long SizeBytes,
    string Sha256);
