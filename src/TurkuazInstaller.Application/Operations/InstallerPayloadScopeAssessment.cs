// Dosya: /src/TurkuazInstaller.Application/Operations/InstallerPayloadScopeAssessment.cs
// Amac: Imzali envanterle okunan current dosyalari arasindaki sahiplik belirsizligini raporlar
// Modul - Service Model CSharp
// Version: 1.0.0
// Aciklama: Dosya hash ve ekstralari ayirir; herhangi bir mutation veya recovery yetkisi tasimaz
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Operations;

public sealed record InstallerPayloadScopeAssessment(
    InstallerPayloadEvidence HashEvidence,
    IReadOnlyList<string> UnattributedCurrentFiles)
{
    // An empty list does not prove complete ownership of the install root.
    // This is never a permission to repair, delete, uninstall or resume.
}
