// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Packages/Velopack/VelopackPackageEngine.cs
// 📌 Amac: Merkezi TurkuazInstaller icin Velopack staging, apply, repair, rollback ve uninstall adapterini uygular
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Dogrulanmis Setup.exe veya full nupkg artifactini atomik stage eder ve resmi Velopack CLI kontratini shell kullanmadan cagirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Infrastructure.Packages.Velopack;

public sealed class VelopackPackageEngine : IPackageEngine
{
    private const int CopyBufferSize = 81920;

    private readonly IProcessRunner _processRunner;

    public VelopackPackageEngine(
        IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<PackageStage> StageAsync(
        PackageRelease release,
        string verifiedArtifactPath,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            verifiedArtifactPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            stagingDirectory);

        if (!File.Exists(verifiedArtifactPath))
        {
            throw new PackageEngineException(
                PackageEngineOperation.Stage,
                "Verified artifact file does not exist.");
        }

        var artifactKind =
            ResolveArtifactKind(release);

        var stageRoot =
            Path.GetFullPath(
                stagingDirectory);

        Directory.CreateDirectory(
            stageRoot);

        var operationDirectory =
            EnsureChildPath(
                stageRoot,
                Path.Combine(
                    stageRoot,
                    Guid.NewGuid()
                        .ToString("N")));

        Directory.CreateDirectory(
            operationDirectory);

        var fileName =
            ResolveArtifactFileName(
                release,
                artifactKind);

        var stagedArtifactPath =
            EnsureChildPath(
                operationDirectory,
                Path.Combine(
                    operationDirectory,
                    fileName));

        var partialPath =
            string.Concat(
                stagedArtifactPath,
                VelopackConventions.PartialSuffix);

        try
        {
            await CopyFileAsync(
                    verifiedArtifactPath,
                    partialPath,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                partialPath,
                stagedArtifactPath);

            return new PackageStage(
                release.PackageId,
                release.Version,
                artifactKind,
                stagedArtifactPath);
        }
        catch (PackageEngineException)
        {
            TryDeleteDirectory(
                operationDirectory);
            throw;
        }
        catch (Exception exception)
            when (
                exception is IOException or
                UnauthorizedAccessException)
        {
            TryDeleteDirectory(
                operationDirectory);

            throw new PackageEngineException(
                PackageEngineOperation.Stage,
                "Artifact could not be staged atomically.",
                innerException: exception);
        }
    }

    public async Task ApplyAsync(
        InstallPlan plan,
        PackageStage stage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(stage);

        ValidateStageMatchesRelease(
            stage,
            plan.Release,
            PackageEngineOperation.Apply);

        ValidatePreservePaths(
            plan.TargetPath,
            plan.PreservePaths);

        if (
            stage.ArtifactKind ==
            PackageArtifactKind.VelopackSetup)
        {
            await RunSetupAsync(
                    plan.TargetPath,
                    stage,
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await RunUpdaterApplyAsync(
                PackageEngineOperation.Apply,
                plan.TargetPath,
                stage,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RepairAsync(
        RepairPlan plan,
        PackageStage stage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(stage);

        ValidateStageMatchesRelease(
            stage,
            plan.Release,
            PackageEngineOperation.Repair);

        RequireFullPackage(
            stage,
            PackageEngineOperation.Repair);

        await RunUpdaterApplyAsync(
                PackageEngineOperation.Repair,
                plan.TargetPath,
                stage,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RollbackAsync(
        RollbackPlan plan,
        PackageStage stage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(stage);

        ValidateStageMatchesRelease(
            stage,
            plan.PreviousRelease,
            PackageEngineOperation.Rollback);

        RequireFullPackage(
            stage,
            PackageEngineOperation.Rollback);

        await RunUpdaterApplyAsync(
                PackageEngineOperation.Rollback,
                plan.TargetPath,
                stage,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UninstallAsync(
        UninstallPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var targetRoot =
            Path.GetFullPath(
                plan.TargetPath);

        var updaterPath =
            Path.Combine(
                targetRoot,
                VelopackConventions.UpdateExecutableName);

        if (!File.Exists(updaterPath))
        {
            throw new PackageEngineException(
                PackageEngineOperation.Uninstall,
                "Velopack Update.exe was not found in the install root.");
        }

        var command =
            new ProcessCommand(
                updaterPath,
                new[]
                {
                    VelopackConventions.SilentArgument,
                    VelopackConventions.RootDirectoryArgument,
                    targetRoot,
                    VelopackConventions.UninstallCommand
                },
                targetRoot);

        await RunCheckedAsync(
                PackageEngineOperation.Uninstall,
                command,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RunSetupAsync(
        string targetPath,
        PackageStage stage,
        CancellationToken cancellationToken)
    {
        EnsureStagedArtifactExists(
            stage,
            PackageEngineOperation.Apply);

        var targetRoot =
            Path.GetFullPath(
                targetPath);

        var command =
            new ProcessCommand(
                stage.ArtifactPath,
                new[]
                {
                    VelopackConventions.SilentArgument,
                    VelopackConventions.InstallToArgument,
                    targetRoot
                },
                Path.GetDirectoryName(
                    stage.ArtifactPath));

        await RunCheckedAsync(
                PackageEngineOperation.Apply,
                command,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RunUpdaterApplyAsync(
        PackageEngineOperation operation,
        string targetPath,
        PackageStage stage,
        CancellationToken cancellationToken)
    {
        EnsureStagedArtifactExists(
            stage,
            operation);

        var targetRoot =
            Path.GetFullPath(
                targetPath);

        var updaterPath =
            Path.Combine(
                targetRoot,
                VelopackConventions.UpdateExecutableName);

        if (!File.Exists(updaterPath))
        {
            throw new PackageEngineException(
                operation,
                "Velopack Update.exe was not found in the install root.");
        }

        var packagesDirectory =
            Path.Combine(
                targetRoot,
                VelopackConventions.PackagesDirectoryName);

        Directory.CreateDirectory(
            packagesDirectory);

        var packageFileName =
            Path.GetFileName(
                stage.ArtifactPath);

        var packagePath =
            EnsureChildPath(
                packagesDirectory,
                Path.Combine(
                    packagesDirectory,
                    packageFileName));

        await CopyAtomicReplaceAsync(
                stage.ArtifactPath,
                packagePath,
                cancellationToken)
            .ConfigureAwait(false);

        var command =
            new ProcessCommand(
                updaterPath,
                new[]
                {
                    VelopackConventions.SilentArgument,
                    VelopackConventions.RootDirectoryArgument,
                    targetRoot,
                    VelopackConventions.PackageDirectoryArgument,
                    packagesDirectory,
                    VelopackConventions.ApplyCommand,
                    VelopackConventions.NoRestartArgument,
                    VelopackConventions.PackageArgument,
                    packagePath
                },
                targetRoot);

        await RunCheckedAsync(
                operation,
                command,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RunCheckedAsync(
        PackageEngineOperation operation,
        ProcessCommand command,
        CancellationToken cancellationToken)
    {
        ProcessResult result;

        try
        {
            result =
                await _processRunner
                    .RunAsync(
                        command,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PackageEngineException(
                operation,
                "Velopack process could not be executed.",
                innerException: exception);
        }

        if (result.ExitCode != 0)
        {
            throw new PackageEngineException(
                operation,
                "Velopack process returned a non-zero exit code.",
                result.ExitCode);
        }
    }

    private static void ValidateStageMatchesRelease(
        PackageStage stage,
        PackageRelease release,
        PackageEngineOperation operation)
    {
        if (
            stage.PackageId != release.PackageId ||
            !stage.Version.Equals(
                release.Version))
        {
            throw new PackageEngineException(
                operation,
                "Staged artifact does not match the requested package release.");
        }
    }

    private static void RequireFullPackage(
        PackageStage stage,
        PackageEngineOperation operation)
    {
        if (
            stage.ArtifactKind !=
            PackageArtifactKind.VelopackFullPackage)
        {
            throw new PackageEngineException(
                operation,
                "Repair and rollback require a Velopack full nupkg artifact.");
        }
    }

    private static void EnsureStagedArtifactExists(
        PackageStage stage,
        PackageEngineOperation operation)
    {
        if (!File.Exists(stage.ArtifactPath))
        {
            throw new PackageEngineException(
                operation,
                "Staged artifact file does not exist.");
        }
    }

    private static PackageArtifactKind ResolveArtifactKind(
        PackageRelease release)
    {
        var path =
            release.Artifact.Uri.IsFile
                ? release.Artifact.Uri.LocalPath
                : release.Artifact.Uri.AbsolutePath;

        var extension =
            Path.GetExtension(path);

        if (
            string.Equals(
                extension,
                VelopackConventions.SetupExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            return PackageArtifactKind.VelopackSetup;
        }

        if (
            string.Equals(
                extension,
                VelopackConventions.FullPackageExtension,
                StringComparison.OrdinalIgnoreCase))
        {
            return PackageArtifactKind.VelopackFullPackage;
        }

        throw new PackageEngineException(
            PackageEngineOperation.Stage,
            "Artifact type is not supported by the Velopack package engine.");
    }

    private static string ResolveArtifactFileName(
        PackageRelease release,
        PackageArtifactKind artifactKind)
    {
        var path =
            release.Artifact.Uri.IsFile
                ? release.Artifact.Uri.LocalPath
                : release.Artifact.Uri.AbsolutePath;

        var fileName =
            Path.GetFileName(
                Uri.UnescapeDataString(path));

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        return artifactKind ==
            PackageArtifactKind.VelopackSetup
                ? VelopackConventions.SetupFallbackName
                : VelopackConventions.FullPackageFallbackName;
    }

    private static void ValidatePreservePaths(
        string targetPath,
        IReadOnlyList<string> preservePaths)
    {
        var targetRoot =
            Path.GetFullPath(
                targetPath);

        var currentRoot =
            Path.GetFullPath(
                Path.Combine(
                    targetRoot,
                    VelopackConventions.CurrentDirectoryName));

        foreach (var preservePath in preservePaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                preservePath);

            if (Path.IsPathRooted(preservePath))
            {
                throw new PackageEngineException(
                    PackageEngineOperation.Apply,
                    "Preserve paths must be relative to the install root.");
            }

            var resolvedPath =
                Path.GetFullPath(
                    Path.Combine(
                        targetRoot,
                        preservePath));

            EnsureChildPath(
                targetRoot,
                resolvedPath);

            if (
                IsPathWithin(
                    currentRoot,
                    resolvedPath))
            {
                throw new PackageEngineException(
                    PackageEngineOperation.Apply,
                    "Preserve paths inside the Velopack current directory are not supported.");
            }
        }
    }

    private static string EnsureChildPath(
        string rootPath,
        string candidatePath)
    {
        var root =
            Path.GetFullPath(
                rootPath);

        var candidate =
            Path.GetFullPath(
                candidatePath);

        if (!IsPathWithin(root, candidate))
        {
            throw new PackageEngineException(
                PackageEngineOperation.Stage,
                "Generated package path escaped the allowed root.");
        }

        return candidate;
    }

    private static bool IsPathWithin(
        string rootPath,
        string candidatePath)
    {
        var comparison =
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        var root =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    rootPath));

        var candidate =
            Path.GetFullPath(
                candidatePath);

        if (
            string.Equals(
                root,
                candidate,
                comparison))
        {
            return true;
        }

        var rootWithSeparator =
            string.Concat(
                root,
                Path.DirectorySeparatorChar);

        return candidate.StartsWith(
            rootWithSeparator,
            comparison);
    }

    private static async Task CopyAtomicReplaceAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (
            PathsEqual(
                sourcePath,
                destinationPath))
        {
            return;
        }

        var partialPath =
            string.Concat(
                destinationPath,
                VelopackConventions.PartialSuffix);

        try
        {
            await CopyFileAsync(
                    sourcePath,
                    partialPath,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                partialPath,
                destinationPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
        }
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source =
            new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        await using var destination =
            new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        await source
            .CopyToAsync(
                destination,
                CopyBufferSize,
                cancellationToken)
            .ConfigureAwait(false);

        await destination
            .FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool PathsEqual(
        string left,
        string right)
    {
        var comparison =
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            comparison);
    }

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(
                    path,
                    recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
