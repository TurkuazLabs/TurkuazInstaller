// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/IBootstrapSelfUpdateDownloader.cs
// 📌 Amac: Kesfedilen bootstrap update artifactini staging alanina indiren ve integrity kontrolu yapan Tool portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Expected size ve SHA-256 dogrulanmadan replacement executable yolu dondurulmez
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public interface IBootstrapSelfUpdateDownloader
{
    Task<string> DownloadAsync(
        BootstrapSelfUpdateRelease release,
        string stagingRoot,
        CancellationToken cancellationToken);
}
