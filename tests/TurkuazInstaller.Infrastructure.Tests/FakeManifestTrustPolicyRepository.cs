// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/FakeManifestTrustPolicyRepository.cs
// 📌 Amac: CMS manifest verifier testlerinde package trust policy repository portunu deterministik fake ile izole eder
// 📌 Modul - Test Repo CSharp
// Version: 1.1.0
// Aciklama: Testin verdigi manifest trust policy degerini async repository kontrati uzerinden dondurur
//
// Bagimli Oldugu Katman: Repo | Tool

using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Tests;

internal sealed class FakeManifestTrustPolicyRepository
    : IManifestTrustPolicyRepository
{
    private readonly ManifestTrustPolicy? _policy;

    public FakeManifestTrustPolicyRepository(
        ManifestTrustPolicy? policy)
    {
        _policy =
            policy;
    }

    public Task<ManifestTrustPolicy?> GetAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            _policy);
    }
}
