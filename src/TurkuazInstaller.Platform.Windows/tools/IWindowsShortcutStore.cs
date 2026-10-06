// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/IWindowsShortcutStore.cs
// 📌 Amac: Windows .lnk create/remove sahiplik davranisini integration managerdan ayiran test seam tanimlar
// 📌 Modul - Tool Port CSharp
// Version: 1.1.0
// Aciklama: Cleanup sirasinda package id + receipt path/hash sahipligini birlikte zorunlu tutan shortcut adapter kontratidir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Platform.Windows.Tools;

public interface IWindowsShortcutStore
{
    Task<WindowsShortcutReceipt> CreateAsync(
        PackageId packageId,
        string targetPath,
        WindowsShortcutIntegration action,
        WindowsShortcutReceipt? existingReceipt,
        CancellationToken cancellationToken);

    Task RemoveOwnedAsync(
        PackageId packageId,
        WindowsShortcutReceipt receipt,
        CancellationToken cancellationToken);
}
