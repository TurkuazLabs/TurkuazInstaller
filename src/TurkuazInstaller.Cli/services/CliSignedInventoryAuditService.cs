// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/services/CliSignedInventoryAuditService.cs
// 📌 Amac: Imzali surum ve imzali dosya envanteri ile kurulu uygulamayi salt-okunur denetler
// 📌 Modul - Service CSharp
// Version: 1.1.0
// Aciklama: Signed baseline ve state kimligi, once/sonra journal hold kontrolu, no mutation authority
// Bagimli Oldugu Katman: Service | Repo | Tool | Domain

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Operations;
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
    private readonly InstallerRecoveryAssessmentService _recoveryAssessment;

    public CliSignedInventoryAuditService(
        IReleaseProvider releaseProvider,
        IManifestSignatureVerifier signatureVerifier,
        IInstallStateRepository stateRepository,
        IInstallerOperationJournalRepository journalRepository,
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
        _recoveryAssessment = new InstallerRecoveryAssessmentService(
            stateRepository,
            journalRepository ?? throw new ArgumentNullException(nameof(journalRepository)));
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

        // Never classify a disk observation as clean when there is any
        // persisted operation checkpoint. Even pre-apply journal entries
        // may be in use concurrently and do not grant audit ownership.
        await RequireNoJournalAsync(packageId, cancellationToken)
            .ConfigureAwait(false);

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

        var report = await _scopeAssessment.AssessAsync(
            verified.PackageId, verified.Version, verified.Channel,
            verified.Files.Select(file =>
                new InstallerExpectedPayloadFile(
                    file.RelativePath, file.Digest)).ToArray(),
            state, state.TargetPath, cancellationToken).ConfigureAwait(false);

        // These are two diagnostic observations, not an atomic snapshot.
        // Refuse a newly opened recovery journal or changed committed
        // metadata rather than emit a stale "observed_clean" report.
        await RequireNoJournalAsync(packageId, cancellationToken)
            .ConfigureAwait(false);
        var stateAfter = await _stateRepository
            .GetAsync(packageId, cancellationToken)
            .ConfigureAwait(false);
        if (stateAfter is null ||
            stateAfter.PackageId != state.PackageId ||
            !stateAfter.Version.Equals(state.Version) ||
            stateAfter.Channel != state.Channel ||
            !string.Equals(
                Path.GetFullPath(stateAfter.TargetPath),
                Path.GetFullPath(state.TargetPath),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Committed installed state changed during read-only audit.");
        }

        return report;
    }

    private async Task RequireNoJournalAsync(
        PackageId packageId, CancellationToken cancellationToken)
    {
        var snapshot = await _recoveryAssessment
            .AssessAsync(packageId, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot.Status != InstallerRecoveryAssessmentStatus.NoJournal)
        {
            throw new InvalidDataException(
                "Signed audit cannot report clean while an operation journal " +
                "exists or committed metadata conflicts: " + snapshot.Status);
        }
    }
}
