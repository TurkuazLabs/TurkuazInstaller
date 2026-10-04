// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Artifacts/IArtifactVerifier.cs
// 📌 Amac: Artifact integrity ve signature verification islemi icin public portu tanimlar
// 📌 Modul - Port CSharp
// Version: 0.3.0
// Aciklama: Verification motorunu Application katmanindan ayirir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Contracts.Artifacts;

public interface IArtifactVerifier
{
    Task<VerificationResult> VerifyAsync(string artifactPath, ArtifactDescriptor expectedArtifact, CancellationToken cancellationToken);
}
