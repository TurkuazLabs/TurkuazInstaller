// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/IBootstrapEnvironmentProbe.cs
// 📌 Amac: Bootstrap prerequisite servisini Windows runtime API detaylarindan ayiran portu tanimlar
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Platform snapshot sorgusunu test edilebilir Tool kontrati haline getirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public interface IBootstrapEnvironmentProbe
{
    BootstrapEnvironmentSnapshot GetSnapshot();
}
