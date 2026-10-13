// Dosya: /src/TurkuazInstaller.Application/Operations/InstallerPayloadScopeAssessmentService.cs
// Amac: Kurulu state ve signed inventory eslesmesini kontrol edip current fazlaliklarini tespit eder
// Modul - Service CSharp
// Version: 1.0.1
// Aciklama: Caller-supplied signed baseline fields, committed state ve current scope read-only karsilastirilir
// Bagimli Oldugu Katman: Service | Domain | Tool

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

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
        PackageId baselinePackageId,
        SemanticVersion baselineVersion,
        ReleaseChannel baselineChannel,
        IReadOnlyList<InstallerExpectedPayloadFile> baselineFiles,
        InstalledPackageState committedState,
        string installRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baselinePackageId);
        ArgumentNullException.ThrowIfNull(baselineVersion);
        ArgumentNullException.ThrowIfNull(baselineFiles);
        ArgumentNullException.ThrowIfNull(committedState);
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var root = Path.GetFullPath(installRoot);
        if (baselinePackageId != committedState.PackageId ||
            baselineVersion != committedState.Version ||
            baselineChannel != committedState.Channel ||
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

        // Caller must provide baseline fields from a separately verified
        // signed inventory. This diagnostic service cannot attest their origin.
        // Only current/ is inventoried for unexpected entries. Known root
        // launcher/updater files are still hashed via signed inventory, but
        // the rest of the install root remains deliberately unclaimed.
        var currentFiles = await _scopeTool.ListCurrentFilesAsync(
            root, cancellationToken).ConfigureAwait(false);
        var comparer = StringComparer.OrdinalIgnoreCase;
        var expected = new HashSet<string>(
            baselineFiles.Select(file => file.RelativePath), comparer);
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
            baselinePackageId,
            root,
            baselineFiles,
            cancellationToken).ConfigureAwait(false);

        return new InstallerPayloadScopeAssessment(
            evidence, Array.AsReadOnly(unattributed));
    }
}
