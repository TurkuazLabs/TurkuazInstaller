// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Localization/ITextCatalog.cs
// 📌 Amac: UI metinlerini language dosyalarindan okumak icin port kontratini tanimlar
// 📌 Modul - Contract CSharp
// Version: 0.1.0
// Aciklama: ViewModel icinde inline kullanici metni bulunmasini engeller
//
// Bagimli Oldugu Katman: Language

namespace TurkuazInstaller.Contracts.Localization;

public interface ITextCatalog
{
    string Get(string key);
}
