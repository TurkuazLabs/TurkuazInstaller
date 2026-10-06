// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/VersionUpdatePolicyService.cs
// 📌 Amac: Signed candidate release surumunu package/channel version policy ile read-only degerlendirir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Exact skip once uygulanir; ardindan maximum version ceiling kontrol edilir; policy yoksa Allowed doner
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Updates;

public sealed class VersionUpdatePolicyService
{
    private readonly IVersionUpdatePolicyRepository
        _repository;

    public VersionUpdatePolicyService(
        IVersionUpdatePolicyRepository repository)
    {
        ArgumentNullException.ThrowIfNull(
            repository);

        _repository = repository;
    }

    public async Task<VersionUpdatePolicyEvaluation> EvaluateAsync(
        PackageId packageId,
        ReleaseChannel channel,
        SemanticVersion candidateVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        ArgumentNullException.ThrowIfNull(
            candidateVersion);

        var policy =
            await _repository
                .GetAsync(
                    packageId,
                    channel,
                    cancellationToken)
                .ConfigureAwait(false);

        if (policy is null)
        {
            return new VersionUpdatePolicyEvaluation(
                VersionUpdatePolicyDecision.Allowed,
                null);
        }

        if (
            policy.SkippedVersions.Any(
                skipped =>
                    skipped.Equals(
                        candidateVersion)))
        {
            return new VersionUpdatePolicyEvaluation(
                VersionUpdatePolicyDecision.Skipped,
                policy);
        }

        if (
            policy.MaximumVersion is not null &&
            candidateVersion >
                policy.MaximumVersion)
        {
            return new VersionUpdatePolicyEvaluation(
                VersionUpdatePolicyDecision.Pinned,
                policy);
        }

        return new VersionUpdatePolicyEvaluation(
            VersionUpdatePolicyDecision.Allowed,
            policy);
    }
}
