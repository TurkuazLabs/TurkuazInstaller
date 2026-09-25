// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Configuration/InstallerUiConfiguration.cs
// 📌 Amac: Runtime UI konfigurasyonunu typed model olarak tasir
// 📌 Modul - Infrastructure CSharp
// Version: 0.1.0
// Aciklama: Default language ve language dizini degerlerini kaynak koddan ayirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Infrastructure.Configuration;

public sealed record InstallerUiConfiguration(
    string DefaultLanguage,
    string LanguageDirectory);
