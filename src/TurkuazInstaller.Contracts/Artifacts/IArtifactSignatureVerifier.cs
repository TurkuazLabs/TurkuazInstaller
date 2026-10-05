// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Artifacts/IArtifactSignatureVerifier.cs
// 📌 Amac: Manifestte signature deklarasyonu bulunan artifactlar icin platform signature verification portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Application katmanini Windows Authenticode implementation detayindan ayirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Contracts.Artifacts;

public interface IArtifactSignatureVerifier
{
    Task<VerificationResult> VerifyAsync(
        string artifactPath,
        ArtifactSignatureDescriptor expectedSignature,
        CancellationToken cancellationToken);
}
