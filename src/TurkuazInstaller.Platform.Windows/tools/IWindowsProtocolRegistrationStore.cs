// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/IWindowsProtocolRegistrationStore.cs
// 📌 Amac: HKCU URL protocol create/remove sahiplik davranisini integration managerdan ayiran test seam tanimlar
// 📌 Modul - Tool Port CSharp
// Version: 1.0.0
// Aciklama: Protocol key yalnız package owner marker eslesirse mutate/delete edilir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Platform.Windows.Tools;

public interface IWindowsProtocolRegistrationStore
{
    Task ApplyAsync(
        PackageId packageId,
        string targetPath,
        WindowsProtocolIntegration action,
        CancellationToken cancellationToken);

    Task RemoveOwnedAsync(
        PackageId packageId,
        string scheme,
        CancellationToken cancellationToken);
}
