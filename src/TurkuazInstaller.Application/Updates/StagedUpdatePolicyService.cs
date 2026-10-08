// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/StagedUpdatePolicyService.cs
// 📌 Amac: Signed rollout provider kararini sadece Eligible halinde update iznine donusturmek
// 📌 Modul - Service CSharp
// Version: 2.5.0
// Aciklama: Yanlis/unknown karar, unavailable gate veya rollback istegi update apply yetkisi vermez
// Bagimli Oldugu Katman: Service | Tool | Model

using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Updates;

public sealed class StagedUpdatePolicyService
{
    private readonly IStagedUpdatePolicyGate _gate;

    public StagedUpdatePolicyService(IStagedUpdatePolicyGate gate)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public async Task RequireEligibleAsync(
        PackageRelease candidateRelease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidateRelease);
        cancellationToken.ThrowIfCancellationRequested();

        // Provider errors propagate and prevent update; no implicit allow fallback.
        var decision = await _gate
            .EvaluateAsync(candidateRelease, cancellationToken)
            .ConfigureAwait(false);

        if (decision != StagedUpdatePolicyDecision.Eligible)
        {
            throw new InstallerStagedUpdatePolicyException(
                candidateRelease, decision);
        }
    }
}
