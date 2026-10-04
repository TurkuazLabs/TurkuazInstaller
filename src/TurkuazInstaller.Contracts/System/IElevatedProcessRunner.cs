// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/IElevatedProcessRunner.cs
// 📌 Amac: UAC gerektiren operasyonlari normal process runnerdan kesin olarak ayiran portu tanimlar
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Bootstrap ve Application kodunun tum processleri otomatik admin olarak calistirmasini engeller
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.System;

public interface IElevatedProcessRunner
{
    Task<ElevationResult> RunElevatedAsync(
        ElevationRequest request,
        CancellationToken cancellationToken);
}
