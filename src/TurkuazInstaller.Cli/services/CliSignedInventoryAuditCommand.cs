// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/services/CliSignedInventoryAuditCommand.cs
// 📌 Amac: Windows CLI icin yerel imzali kurulum dosya envanteri salt-okunur denetim komutu
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Strict audit args, bounded file reads, pinned CMS verification and JSON report; never repairs
// Bagimli Oldugu Katman: Controller | Service | Repo | Tool

using System.Text.Json;
using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Cli.Config;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.File;
using TurkuazInstaller.Infrastructure.Repositories;
using TurkuazInstaller.Infrastructure.Tools;

namespace TurkuazInstaller.Cli.Services;

public static class CliSignedInventoryAuditCommand
{
    private const int BufferSize = 81920;

    public static async Task<CliExitCode> RunAsync(
        IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        (PackageId Package, ReleaseChannel Channel, string Manifest, string Inventory) invocation;
        try
        {
            invocation = Parse(args);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            Console.Error.WriteLine("Invalid audit invocation: " + ex.Message);
            return CliExitCode.InvalidInvocation;
        }

        try
        {
            var options = CliPathDefaults.CreateRuntimeOptions();
            var trustStore = new YamlManifestTrustPolicyRepository(
                new YamlManifestTrustPolicyRepositoryOptions(
                    options.ManifestTrustStorePath));
            var verifier = new CmsManifestSignatureVerifier(trustStore);
            var manifestPath = Path.GetFullPath(invocation.Manifest);
            var provider = new FileReleaseProvider(
                new InstallerManifestReader(), verifier,
                new FileReleaseProviderOptions(manifestPath, manifestPath));
            var stateRepository = new JsonInstallStateRepository(
                new JsonInstallStateRepositoryOptions(options.StateRoot));
            var audit = new CliSignedInventoryAuditService(
                provider, verifier, stateRepository,
                new InstallerPayloadScopeAssessmentService(
                    new InstalledPayloadScopeTool(),
                    new InstallerPayloadEvidenceService(
                        new InstalledPayloadHashTool())));

            var inventoryPath = Path.GetFullPath(invocation.Inventory);
            var inventory = await ReadBoundedFileAsync(
                inventoryPath,
                ManifestContentLimits.MaximumManifestBytes,
                cancellationToken).ConfigureAwait(false);
            var signature = await ReadBoundedFileAsync(
                ManifestSignatureConventions.GetDetachedSignaturePath(inventoryPath),
                ManifestContentLimits.MaximumDetachedSignatureBytes,
                cancellationToken).ConfigureAwait(false);

            var result = await audit.AssessAsync(
                invocation.Package, invocation.Channel,
                inventory, signature, cancellationToken).ConfigureAwait(false);
            var mismatches = result.HashEvidence.Files
                .Where(file =>
                    file.Status != InstallerPayloadFileStatus.MatchesSuppliedDigest)
                .Select(file => new
                {
                    path = file.RelativePath,
                    status = file.Status.ToString()
                }).ToArray();
            var clean = mismatches.Length == 0 &&
                result.UnattributedCurrentFiles.Count == 0;

            Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                diagnostic_only = true,
                permits_repair = false,
                package_id = result.HashEvidence.PackageId.Value,
                install_root = result.HashEvidence.InstallRoot,
                inspected_signed_files = result.HashEvidence.Files.Count,
                matching_signed_files = result.HashEvidence.Files.Count - mismatches.Length,
                mismatches,
                unattributed_current_files = result.UnattributedCurrentFiles,
                observed_clean = clean,
                unscanned_root_files_and_user_data = true
            }));
            // "Clean" is an observation in this limited scope, never
            // a permission to clear the journal or change installed state.
            return clean ? CliExitCode.Success : CliExitCode.OperationFailed;
        }
        catch (OperationCanceledException)
        {
            return CliExitCode.Cancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Signed inventory audit failed closed: " + ex.Message);
            return CliExitCode.OperationFailed;
        }
    }

    private static (
        PackageId Package,
        ReleaseChannel Channel,
        string Manifest,
        string Inventory) Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Count == 0 ||
            !string.Equals(args[0], "audit", StringComparison.Ordinal))
        {
            throw new FormatException("Audit operation is required.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i++)
        {
            var option = args[i];
            if (option is not ("--package" or "--manifest" or
                "--inventory" or "--channel") ||
                i + 1 >= args.Count ||
                values.ContainsKey(option))
            {
                throw new FormatException("Unknown, repeated or incomplete audit option.");
            }
            var value = args[++i];
            if (string.IsNullOrWhiteSpace(value) ||
                value.StartsWith("--", StringComparison.Ordinal))
            {
                throw new FormatException("Audit option value is missing.");
            }
            values.Add(option, value.Trim());
        }

        if (!values.TryGetValue("--package", out var package) ||
            !values.TryGetValue("--manifest", out var manifest) ||
            !values.TryGetValue("--inventory", out var inventory))
        {
            throw new FormatException(
                "Audit requires --package, --manifest and --inventory.");
        }

        var channel = ReleaseChannel.Stable;
        if (values.TryGetValue("--channel", out var requestedChannel))
        {
            channel = requestedChannel switch
            {
                "stable" => ReleaseChannel.Stable,
                "beta" => ReleaseChannel.Beta,
                _ => throw new FormatException(
                    "Audit channel must be stable or beta.")
            };
        }

        if (!File.Exists(manifest) || !File.Exists(inventory))
        {
            throw new FormatException(
                "Audit requires existing local manifest and inventory files.");
        }

        return (PackageId.Parse(package), channel, manifest, inventory);
    }

    private static async Task<byte[]> ReadBoundedFileAsync(
        string path, int maximumBytes, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (source.Length == 0 || source.Length > maximumBytes)
        {
            throw new InvalidDataException(
                "Signed inventory data or signature is empty or exceeds limits.");
        }

        using var output = new MemoryStream(
            (int)Math.Min(source.Length, BufferSize));
        var buffer = new byte[BufferSize];
        long count = 0;
        while (true)
        {
            var n = await source.ReadAsync(
                buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                break;
            }
            if (n > maximumBytes - count)
            {
                throw new InvalidDataException(
                    "Signed inventory data or signature exceeds limits.");
            }
            output.Write(buffer, 0, n);
            count += n;
        }
        return output.ToArray();
    }
}
