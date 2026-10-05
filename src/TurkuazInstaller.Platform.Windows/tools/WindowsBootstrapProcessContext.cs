// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsBootstrapProcessContext.cs
// 📌 Amac: Calisan bootstrap executable yolunu Windows process ortamindan cozer
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Bootstrap Service icin platforma ozel executable path adapterini uygular
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsBootstrapProcessContext
    : IBootstrapProcessContext
{
    public string GetCurrentExecutablePath()
    {
        var path =
            Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "Current bootstrap executable path could not be resolved.");
        }

        return Path.GetFullPath(path);
    }
}
