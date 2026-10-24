// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/PinnedThemeTrustPolicyRepository.cs
// Amac: Tema imzalayanlarini kurulumun salt-okunur allowlist'i ile sinirlamak
// Modul - CSharp Repository Adapter
// Version: 0.1.0-qa
// Aciklama: TPK/user YAML, environment veya diskten otomatik trust enrollment yapmaz
// Bagimli Oldugu Katman: Contracts | Domain | Cryptographic Trust

using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Domain.Manifests;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Branding;

/// <summary>
/// Immutable, caller-supplied trusted publisher pins. Production callers must
/// construct these from immutable, reviewed application source/binary. Theme
/// packages and per-user YAML must NEVER populate or override this repository.
/// </summary>
public sealed class PinnedThemeTrustPolicyRepository : IManifestTrustPolicyRepository
{
    private readonly IReadOnlyDictionary<string, ManifestTrustPolicy> _trusted;

    public PinnedThemeTrustPolicyRepository(
        IEnumerable<KeyValuePair<string, ManifestTrustPolicy>> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);

        var copy = new Dictionary<string, ManifestTrustPolicy>(StringComparer.Ordinal);
        foreach (var (package, policy) in pins)
        {
            if (policy is null)
            {
                throw new ArgumentException("A publisher trust entry is null.", nameof(pins));
            }

            var id = PackageId.Parse(package).Value;
            if (!copy.TryAdd(id, policy))
            {
                throw new ArgumentException("Duplicated trusted product identity.", nameof(pins));
            }
        }

        // Recopy to make subsequent mutations of caller dictionaries ineffective.
        _trusted = new System.Collections.ObjectModel
            .ReadOnlyDictionary<string, ManifestTrustPolicy>(copy);
    }

    public Task<ManifestTrustPolicy?> GetAsync(
        PackageId packageId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        cancellationToken.ThrowIfCancellationRequested();

        _trusted.TryGetValue(packageId.Value, out var policy);
        return Task.FromResult(policy);
    }
}
