// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerRecoveryRequiredException.cs
// 📌 Amac: Yarim kalmis paket degisikliklerinden sonra otomatik mutasyonu guvenli bicimde durdurur
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Journal checkpoint incelemesi gerektiren paketler icin typed recovery gate
// Bagimli Oldugu Katman: Service | Domain

using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Application.Operations;

public sealed class InstallerRecoveryRequiredException : InvalidOperationException
{
    public InstallerRecoveryRequiredException(
        PackageId packageId,
        InstallerOperationPhase phase,
        InstallerOperationPhase? interruptedPhase)
        : base(
            "An interrupted installer operation may have modified the installed package. " +
            "Automatic retry is blocked until explicit recovery verifies disk, " +
            "committed state and trusted package artifacts.")
    {
        PackageId = packageId;
        Phase = phase;
        InterruptedPhase = interruptedPhase;
    }

    public PackageId PackageId { get; }

    public InstallerOperationPhase Phase { get; }

    public InstallerOperationPhase? InterruptedPhase { get; }
}
