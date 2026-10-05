// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/IInstallerEventLogger.cs
// 📌 Amac: Installer structured diagnostic eventlerini yazan logger portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.1.0
// Aciklama: Application workflow'unu JSONL, ETW veya baska log sink implementasyonlarindan ayirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Operations;

namespace TurkuazInstaller.Contracts.Operations;

public interface IInstallerEventLogger
{
    Task WriteAsync(
        InstallerEventEntry entry,
        CancellationToken cancellationToken);
}
