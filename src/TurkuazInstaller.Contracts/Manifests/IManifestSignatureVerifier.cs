// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Manifests/IManifestSignatureVerifier.cs
// 📌 Amac: Detached installer manifest imzasinin kriptografik ve pinned publisher dogrulamasini Tool adapterindan ayirir
// 📌 Modul - Tool Port CSharp
// Version: 1.1.0
// Aciklama: Ham manifest byte'lari ve detached signature uzerinden package bazli fail-closed verification sonucu uretir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Contracts.Manifests;

public interface IManifestSignatureVerifier
{
    Task<VerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> manifestContent,
        ReadOnlyMemory<byte> detachedSignature,
        PackageId packageId,
        CancellationToken cancellationToken);
}
