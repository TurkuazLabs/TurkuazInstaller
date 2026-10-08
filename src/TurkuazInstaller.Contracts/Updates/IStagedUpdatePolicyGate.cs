// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Updates/IStagedUpdatePolicyGate.cs
// 📌 Amac: Community update workflow ile optional signed commercial rollout authority arasinda port tanimlamak
// 📌 Modul - Port CSharp
// Version: 2.5.0
// Aciklama: Pro bagimliligi olmadan latest signed release icin fail-closed update eligibility sorgular
// Bagimli Oldugu Katman: Service | Tool | Model

using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Updates;

public interface IStagedUpdatePolicyGate
{
    Task<StagedUpdatePolicyDecision> EvaluateAsync(
        PackageRelease candidateRelease,
        CancellationToken cancellationToken);
}
