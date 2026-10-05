// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Operations/InstallerOperationJournalEntry.cs
// 📌 Amac: Devam eden veya yarim kalmis installer operasyonunun kalici journal snapshotini tasir
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Package, operation, version, target, phase, timestamp ve failure bilgisini immutable typed modelde toplar
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Domain.Operations;

public sealed record InstallerOperationJournalEntry(
    Guid OperationId,
    PackageId PackageId,
    InstallerOperationType Operation,
    string? Version,
    string TargetPath,
    InstallerOperationPhase Phase,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? Failure);
