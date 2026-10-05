// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopRuntimeOptions.cs
// 📌 Amac: WinUI runtime state, staging ve cross-process lock dizinlerini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.1.0
// Aciklama: Desktop runtime storage yollarini Service, Repo ve operation lock implementasyonlarindan ayirir
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.WinUI.Config;

public sealed record DesktopRuntimeOptions(
    string StateRoot,
    string StagingRoot,
    string LockRoot);
