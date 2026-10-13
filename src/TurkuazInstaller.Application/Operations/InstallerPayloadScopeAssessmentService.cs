// Dosya: /src/TurkuazInstaller.Application/Operations/InstallerPayloadScopeAssessmentService.cs
// Amac: Kurulu state ve signed inventory eslesmesini kontrol edip current fazlaliklarini tespit eder
// Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Yalnizca read-only hash, signed baseline kimligi ve current scope karsilastirmasi yapar
// Bagimli Oldugu Katman: Service | Domain | Tool

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Infrastructure.Manifests;

namespace TurkuazInstaller.Application.Operations;

public sealed class InstallerPayloadScopeAssessmentService
{
    private readonly IInstalledPayloadScopeTool _scopeTool;
    private readonly InstallerPayloadEvidenceService _evidence;

    public InstallerPayloadScopeAssessmentService(
        IInstalledPayloadScopeTool scopeTool,
        InstallerPayloadEvidenceService evidence)
    {
        _scopeTool = scopeTool ?? throw new ArgumentNullException(nameof(scopeTool));
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
    }

    public async Task<InstallerPayloadScopeAssessment> AssessAsync(
        SignedPayloadInventory signedInventory,
        InstalledPackageState committedState,
        string installRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signedInventory);
        ArgumentNullException.ThrowIfNull(committedState);
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var root = Path.GetFullPath(installRoot);
        if (signedInventory.PackageId != committedState.PackageId ||
            signedInventory.Version != committedState.Version ||
            signedInventory.Channel != committedState.Channel ||
            !string.Equals(
                Path.TrimEndingDirectorySeparator(root),
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(committedState.TargetPath)),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Signed payload inventory and committed install state disagree.");
        }

        // Only current/ is inventoried for unexpected entries. Known root
        // launcher/updater files are still hashed via signed inventory, but
        // the rest of the install root remains deliberately unclaimed.
        var currentFiles = await _scopeTool.ListCurrentFilesAsync(
            root, cancellationToken).ConfigureAwait(false);
        var comparer = StringComparer.OrdinalIgnoreCase;
        var expected = new HashSet<string>(
            signedInventory.Files.Select(file => file.RelativePath), comparer);
        if (currentFiles.Count != currentFiles.Distinct(comparer).Count())
        {
            throw new InvalidDataException(
                "Installed current scope contains ambiguous duplicate paths.");
        }

        var unattributed = currentFiles
            .Where(path => !expected.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var evidence = await _evidence.AssessAsync(
            signedInventory.PackageId,
            root,
            signedInventory.Files.Select(file =>
                new InstallerExpectedPayloadFile(
                    file.RelativePath, file.Digest)).ToArray(),
            cancellationToken).ConfigureAwait(false);

        return new InstallerPayloadScopeAssessment(
            evidence, Array.AsReadOnly(unattributed));
    }
}
