// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/IBootstrapFileCleaner.cs
// 📌 Amac: Self-update sonrasi staged bootstrap dosyasini platform Tool katmaninda temizlemek icin port tanimlar
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Bootstrap runtime servisinin dogrudan dosya sistemi kullanmasini engeller
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public interface IBootstrapFileCleaner
{
    Task TryDeleteAsync(
        string filePath,
        CancellationToken cancellationToken);
}
