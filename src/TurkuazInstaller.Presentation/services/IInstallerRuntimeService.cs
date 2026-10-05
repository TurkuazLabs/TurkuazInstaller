// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/IInstallerRuntimeService.cs
// 📌 Amac: Presentation Service katmanini provider, repository ve Tool implementasyonlarindan ayiran runtime portunu tanimlar
// 📌 Modul - Service CSharp
// Version: 1.1.0
// Aciklama: Desktop request, reboot resume ve Application progress kontratlariyla gercek installer runtime operasyonunu soyutlar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Presentation.ViewModels;

namespace TurkuazInstaller.Presentation.Services;

public interface IInstallerRuntimeService
{
    Task ExecuteAsync(
        InstallerDesktopRequest request,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken);

    Task ResumeAsync(
        PackageId packageId,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken);
}
