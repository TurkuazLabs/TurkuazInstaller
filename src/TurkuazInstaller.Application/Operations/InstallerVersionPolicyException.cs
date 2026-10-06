// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Operations/InstallerVersionPolicyException.cs
// 📌 Amac: Update mutationinin version skip/pinning policy tarafindan engellendigini typed exception ile tasir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Package, candidate surum, decision ve optional maximum version contextini UI/CLI katmanina tasir
//
// Bagimli Oldugu Katman: Service | View

using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Operations;

public sealed class InstallerVersionPolicyException
    : InvalidOperationException
{
    public InstallerVersionPolicyException(
        PackageId packageId,
        SemanticVersion candidateVersion,
        VersionUpdatePolicyDecision decision,
        SemanticVersion? maximumVersion)
        : base(
            CreateMessage(
                candidateVersion,
                decision,
                maximumVersion))
    {
        PackageId = packageId;
        CandidateVersion = candidateVersion;
        Decision = decision;
        MaximumVersion = maximumVersion;
    }

    public PackageId PackageId { get; }

    public SemanticVersion CandidateVersion { get; }

    public VersionUpdatePolicyDecision Decision { get; }

    public SemanticVersion? MaximumVersion { get; }

    private static string CreateMessage(
        SemanticVersion candidateVersion,
        VersionUpdatePolicyDecision decision,
        SemanticVersion? maximumVersion)
    {
        return decision switch
        {
            VersionUpdatePolicyDecision.Skipped =>
                string.Concat(
                    "Update version ",
                    candidateVersion,
                    " is skipped by version policy."),
            VersionUpdatePolicyDecision.Pinned =>
                string.Concat(
                    "Update version ",
                    candidateVersion,
                    " exceeds pinned maximum version ",
                    maximumVersion?.ToString()
                    ?? "(unknown)",
                    "."),
            _ =>
                "Update is blocked by version policy."
        };
    }
}
