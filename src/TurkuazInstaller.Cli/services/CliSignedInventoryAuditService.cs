// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/services/CliSignedInventoryAuditService.cs
// 📌 Amac: Imzali surum ve imzali dosya envanteri ile kurulu uygulamayi salt-okunur denetler
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Signed trust -> committed state -> real disk evidence, no mutation or recovery authorization
// Bagimli Oldugu Katman: Service | Repo | Tool | Domain

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Cli.Services;

public sealed class CliSignedInventoryAuditService
{
    private readonly IReleaseProvider _releaseProvider;
    private readonly SignedPayloadInventoryReader _inventoryReader;
    private readonly IInstallStateRepository _stateRepository;
    private readonly InstallerPayloadScopeAssessmentService _scopeAssessment;

    public CliSignedInventoryAuditService(
        IReleaseProvider releaseProvider,
        IManifestSignatureVerifier signatureVerifier,
        IInstallStateRepository stateRepository,
        InstallerPayloadScopeAssessmentService scopeAssessment)
    {
        _releaseProvider = releaseProvider ??
            throw new ArgumentNullException(nameof(releaseProvider));
        _inventoryReader = new SignedPayloadInventoryReader(
            signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier)));
        _stateRepository = stateRepository ??
            throw new ArgumentNullException(nameof(stateRepository));
        _scopeAssessment = scopeAssessment ??
            throw new ArgumentNullException(nameof(scopeAssessment));
    }

    public async Task<InstallerPayloadScopeAssessment> AssessAsync(
        PackageId packageId,
        ReleaseChannel channel,
        ReadOnlyMemory<byte> inventoryBytes,
        ReadOnlyMemory<byte> inventorySignature,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        cancellationToken.ThrowIfCancellationRequested();

        // A missing committed state must NEVER resolve to an
        // alternate directory or optimistic fresh-install assessment.
        var state = await _stateRepository
            .GetAsync(packageId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "Installed state missing: signed audit cannot claim installation ownership.");

        // Only the existing signed-manifest provider can supply the
        // release baseline. Never synthesize a release from JSON fields.
        var release = await _releaseProvider
            .GetLatestReleaseAsync(packageId, channel, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException(
                "Trusted signed release is unavailable for the requested channel.");

        if (release.PackageId != packageId ||
            release.Channel != channel ||
            !release.Version.Equals(state.Version) ||
            state.Channel != channel)
        {
            throw new InvalidDataException(
                "Signed release does not match committed package, channel and version.");
        }

        // CMS + configured publisher/certificate pin + complete signed
        // package/version/channel/artifact identity validation first.
        // No filesystem reads occur when this trust gate rejects input.
        var verified = await _inventoryReader.ReadAsync(
            inventoryBytes, inventorySignature, release, cancellationToken)
            .ConfigureAwait(false);

        return await _scopeAssessment.AssessAsync(
            verified.PackageId, verified.Version, verified.Channel,
            verified.Files.Select(file =>
                new InstallerExpectedPayloadFile(
                    file.RelativePath, file.Digest)).ToArray(),
            state, state.TargetPath, cancellationToken).ConfigureAwait(false);
    }
}
