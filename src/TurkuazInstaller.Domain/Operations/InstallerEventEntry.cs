// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Operations/InstallerEventEntry.cs
// 📌 Amac: Installer structured log kaydini typed Domain modeli olarak tasir
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Operation correlation, package, phase, severity, event name ve error metadata'sini tek kayitta toplar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Domain.Operations;

public sealed record InstallerEventEntry(
    DateTimeOffset TimestampUtc,
    Guid OperationId,
    PackageId PackageId,
    InstallerOperationType Operation,
    InstallerOperationPhase Phase,
    InstallerEventLevel Level,
    string EventName,
    string Message,
    string? ErrorType);
