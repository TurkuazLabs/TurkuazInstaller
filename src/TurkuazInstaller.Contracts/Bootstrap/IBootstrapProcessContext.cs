// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/IBootstrapProcessContext.cs
// 📌 Amac: Calisan bootstrap executable yolunu platform Tool adapterinden almak icin port tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Self-update ve app launch root cozumunu Environment API detayindan ayirir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public interface IBootstrapProcessContext
{
    string GetCurrentExecutablePath();
}
