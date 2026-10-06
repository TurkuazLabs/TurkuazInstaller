// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsIntegrationReceipt.cs
// 📌 Amac: Package-owned Windows shortcut/protocol entegrasyon receipt verisini typed modelde tasir
// 📌 Modul - Tool Model CSharp
// Version: 1.0.0
// Aciklama: Shortcut path+SHA-256 sahipligi ve protocol scheme receipt bilgisini cleanup icin saklar
//
// Bagimli Oldugu Katman: Tool | Repo

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed record WindowsShortcutReceipt(
    string ActionId,
    string Path,
    string Sha256);

public sealed record WindowsIntegrationReceipt(
    PackageId PackageId,
    IReadOnlyList<WindowsShortcutReceipt> Shortcuts,
    IReadOnlyList<string> ProtocolSchemes);
