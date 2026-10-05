// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/IBootstrapApplicationLauncher.cs
// 📌 Amac: Bootstrap sonrasi desktop uygulamasini baslatan Tool adapteri portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Bootstrap Service katmanini Windows Process API detayindan ayirir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public interface IBootstrapApplicationLauncher
{
    Task LaunchAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}
