// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/IRebootResumeScheduler.cs
// 📌 Amac: Reboot isteyen prerequisite calismadan once installer relaunch kaydini yoneten Tool portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Application katmanini Windows RunOnce gibi platform-specific relaunch mekanizmalarindan ayirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Contracts.System;

public interface IRebootResumeScheduler
{
    Task ScheduleAsync(
        PackageId packageId,
        CancellationToken cancellationToken);

    Task CancelAsync(
        PackageId packageId,
        CancellationToken cancellationToken);
}
