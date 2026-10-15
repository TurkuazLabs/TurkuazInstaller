// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerPayloadEvidence.cs
// 📌 Amac: Kurulu dosya hash karsilastirmasini yetki uretmeyen typed raporla tasir
// 📌 Modul - Model CSharp
// Version: 1.0.0
// Aciklama: Yalnizca supplied baseline ile eslesme bildirir, trust veya recovery izni vermez
// Bagimli Oldugu Katman: Service | Domain

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Application.Operations;

public sealed record InstallerExpectedPayloadFile(
    string RelativePath,
    ArtifactDigest ExpectedDigest);

public enum InstallerPayloadFileStatus
{
    MatchesSuppliedDigest,
    Missing,
    DigestMismatch
}

public sealed record InstallerPayloadFileEvidence(
    string RelativePath,
    string ExpectedSha256,
    string? ActualSha256,
    InstallerPayloadFileStatus Status);

public sealed record InstallerPayloadEvidence(
    PackageId PackageId,
    string InstallRoot,
    IReadOnlyList<InstallerPayloadFileEvidence> Files)
{
    // This comparison is not proof that the baseline was signed, complete,
    // or belongs to this installed version. It grants no repair permission.
}
