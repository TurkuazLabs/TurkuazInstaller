// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/IInstallerRuntimeService.cs
// 📌 Amac: Presentation Service katmanini provider, repository ve Tool implementasyonlarindan ayiran runtime portunu tanimlar
// 📌 Modul - Service CSharp
// Version: 1.2.0
// Aciklama: Desktop mutation, read-only update discovery ve reboot resume kontratlariyla gercek installer runtime operasyonunu soyutlar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Presentation.ViewModels;

namespace TurkuazInstaller.Presentation.Services;

public interface IInstallerRuntimeService
{
    Task<UpdateCheckResult> CheckUpdateAsync(
        InstallerUpdateCheckRequest request,
        CancellationToken cancellationToken);

    Task ExecuteAsync(
        InstallerDesktopRequest request,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken);

    Task ResumeAsync(
        PackageId packageId,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken);
}
