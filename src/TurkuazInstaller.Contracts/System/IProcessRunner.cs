// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/IProcessRunner.cs
// 📌 Amac: Harici executable calistirma davranisini Package Engine adapterindan ayiran portu tanimlar
// 📌 Modul - Port CSharp
// Version: 0.5.0
// Aciklama: Velopack Setup.exe ve Update.exe cagrisini test edilebilir ve shell bagimsiz hale getirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.System;

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        ProcessCommand command,
        CancellationToken cancellationToken);
}
