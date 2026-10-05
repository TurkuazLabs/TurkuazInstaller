// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/services/ICliInstallerRuntimeService.cs
// 📌 Amac: CLI Service katmanini concrete provider, repository ve Tool implementasyonlarindan ayirir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Normal mutation ve reboot resume operasyonlarini typed CLI invocation ile soyutlar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Cli.Services;

public interface ICliInstallerRuntimeService
{
    Task ExecuteAsync(
        CliInvocation invocation,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken);

    Task ResumeAsync(
        PackageId packageId,
        IProgress<InstallerOperationProgress> progress,
        CancellationToken cancellationToken);
}
