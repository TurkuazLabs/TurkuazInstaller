// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/IWindowsRunOnceStore.cs
// 📌 Amac: Reboot resume schedulerini Windows Registry API detayindan ayiran Tool portunu tanimlar
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: RunOnce value yazma ve silme islemlerini test edilebilir adapter sinirina tasir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Platform.Windows.Tools;

public interface IWindowsRunOnceStore
{
    void Set(
        string valueName,
        string commandLine);

    void Delete(
        string valueName);
}
