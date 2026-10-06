// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/IWindowsCredentialReader.cs
// 📌 Amac: Windows generic credential secret okumasini Provider credential resolverdan ayiran Tool seam tanimlar
// 📌 Modul - Tool Port CSharp
// Version: 1.0.0
// Aciklama: Credential Manager P/Invoke detayini test edilebilir read-only arayuz arkasinda tutar
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Platform.Windows.Tools;

public interface IWindowsCredentialReader
{
    string? ReadGenericSecret(
        string targetName);
}
