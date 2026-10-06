// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Integrations/WindowsShortcutLocation.cs
// 📌 Amac: Windows shortcut hedef lokasyonlarini typed Domain enum olarak tanimlar
// 📌 Modul - Domain CSharp
// Version: 1.0.0
// Aciklama: Desktop ve Start Menu lokasyonlarini magic string disina tasir
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Domain.Integrations;

public enum WindowsShortcutLocation
{
    Desktop = 0,
    StartMenu = 1
}
