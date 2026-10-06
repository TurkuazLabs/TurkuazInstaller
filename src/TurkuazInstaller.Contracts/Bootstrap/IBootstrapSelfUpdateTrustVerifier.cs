// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/IBootstrapSelfUpdateTrustVerifier.cs
// 📌 Amac: Bootstrap self-update icin mevcut executable trust anchorini ve replacement signer eslesmesini dogrulayan Tool portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.0.0
// Aciklama: Unsigned/untrusted current bootstrapta auto-update'i devre disi birakir; replacement icin typed verification sonucu dondurur
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Contracts.Bootstrap;

public interface IBootstrapSelfUpdateTrustVerifier
{
    Task<bool> CanSelfUpdateAsync(
        string currentExecutablePath,
        CancellationToken cancellationToken);

    Task<VerificationResult> VerifyReplacementAsync(
        string currentExecutablePath,
        string replacementExecutablePath,
        CancellationToken cancellationToken);
}
