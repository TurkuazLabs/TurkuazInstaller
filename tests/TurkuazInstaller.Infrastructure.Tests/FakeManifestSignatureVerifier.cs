// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/FakeManifestSignatureVerifier.cs
// 📌 Amac: Provider contract testlerinde detached manifest verification portunu deterministik fake ile izole eder
// 📌 Modul - Test Tool CSharp
// Version: 1.1.0
// Aciklama: Verification sonucunu dis kriptografiye cikmadan dondurur ve cagrilan package id bilgisini testlere aciklar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Verification;

namespace TurkuazInstaller.Infrastructure.Tests;

internal sealed class FakeManifestSignatureVerifier
    : IManifestSignatureVerifier
{
    private readonly VerificationResult _result;

    public FakeManifestSignatureVerifier(
        VerificationResult? result = null)
    {
        _result =
            result ??
            VerificationResult.Passed();
    }

    public int CallCount { get; private set; }

    public PackageId? LastPackageId { get; private set; }

    public Task<VerificationResult> VerifyAsync(
        ReadOnlyMemory<byte> manifestContent,
        ReadOnlyMemory<byte> detachedSignature,
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        LastPackageId =
            packageId;

        return Task.FromResult(
            _result);
    }
}
