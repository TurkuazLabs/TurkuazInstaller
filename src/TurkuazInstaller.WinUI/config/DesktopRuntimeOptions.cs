// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopRuntimeOptions.cs
// 📌 Amac: WinUI runtime state ve staging dizinlerini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 0.7.0
// Aciklama: Desktop runtime storage yollarini Service ve Repo implementasyonlarindan ayirir
//
// Bagimli Oldugu Katman: Config

namespace TurkuazInstaller.WinUI.Config;

public sealed record DesktopRuntimeOptions(
    string StateRoot,
    string StagingRoot);
