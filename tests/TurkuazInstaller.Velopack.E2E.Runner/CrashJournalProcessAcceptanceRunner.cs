// 📄 Dosya Yolu: /tests/TurkuazInstaller.Velopack.E2E.Runner/CrashJournalProcessAcceptanceRunner.cs
// 📌 Amac: GitHub-hosted Windows'ta gercek workflow process kill ve Applying checkpoint recovery-hold E2E
// 📌 Modul - Test CSharp
// Version: 1.3.0
// Aciklama: Update.exe oncesi, sonrasi ve aktif process kesintisinde journal, state ve user data korumasini denetler
// Bagimli Oldugu Katman: Service | Repo | Tool | CI

using System.Diagnostics;
using System.Security.Cryptography;
using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Artifacts;
using TurkuazInstaller.Infrastructure.Operations;
using TurkuazInstaller.Infrastructure.Packages.Velopack;
using TurkuazInstaller.Infrastructure.Processes;
using TurkuazInstaller.Infrastructure.Repositories;

namespace TurkuazInstaller.Velopack.E2E.Runner;

internal static class CrashJournalProcessAcceptanceRunner
{
    private const string PackageName = "turkuazlabs.turkuazinstaller.e2e";
    private const string VersionOne = "1.0.0";
    private const string VersionTwo = "2.0.0";
    private const string SignalText = "UPDATE_APPLY_READY";
    private const string SignalAfterApplyText = "UPDATER_APPLIED_BEFORE_STATE_COMMIT";
    private const string SignalWhileRunningText = "UPDATER_PROCESS_RUNNING";
    private const string UpdaterExecutableName = "Update";
    private const char SignalSeparator = '|';

    private enum CrashBoundaryMode
    {
        BeforeUpdater,
        AfterUpdater,
        DuringUpdater
    }
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(60);

    public static async Task RunAsync(
        string setupV1,
        string fullV2,
        string parentStagingRoot,
        string versionMarker,
        string userDataDirectory,
        string userDataMarker)
    {
        await RunScenarioAsync(
            setupV1, fullV2, parentStagingRoot, versionMarker,
            userDataDirectory, userDataMarker, mode: CrashBoundaryMode.BeforeUpdater);
        await RunScenarioAsync(
            setupV1, fullV2, parentStagingRoot, versionMarker,
            userDataDirectory, userDataMarker, mode: CrashBoundaryMode.AfterUpdater);
        await RunScenarioAsync(
            setupV1, fullV2, parentStagingRoot, versionMarker,
            userDataDirectory, userDataMarker, mode: CrashBoundaryMode.DuringUpdater);
    }

    private static async Task RunScenarioAsync(
        string setupV1,
        string fullV2,
        string parentStagingRoot,
        string versionMarker,
        string userDataDirectory,
        string userDataMarker,
        CrashBoundaryMode mode)
    {
        if (!OperatingSystem.IsWindows() ||
            !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                "true", StringComparison.Ordinal) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT"),
                "github-hosted", StringComparison.Ordinal))
        {
            Console.WriteLine("TURKUAZ_INSTALLER_PROCESS_KILL_LAB_SKIPPED_NOT_HOSTED");
            return;
        }

        // Only a disposable fixture inside the already isolated E2E staging
        // root can be deleted. Never point this at a user application.
        var root = Path.Combine(
            Path.GetFullPath(parentStagingRoot),
            mode switch
            {
                CrashBoundaryMode.BeforeUpdater => "crash-journal-lab",
                CrashBoundaryMode.AfterUpdater => "crash-post-updater-lab",
                CrashBoundaryMode.DuringUpdater => "crash-running-updater-lab",
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            });
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
        Directory.CreateDirectory(root);
        var installRoot = Path.Combine(root, "install");
        var stageRoot = Path.Combine(root, "staging");
        var stateRoot = Path.Combine(root, "state");
        var journalRoot = Path.Combine(root, "journal");
        var signal = Path.Combine(root, "apply-ready.signal");
        var marker = Path.Combine(installRoot, "current", versionMarker);
        var dataFile = Path.Combine(installRoot, userDataDirectory, userDataMarker);
        var userBytes = Guid.NewGuid().ToString("N");
        var packageId = PackageId.Parse(PackageName);
        var original = CreateRelease(setupV1, VersionOne);
        var update = CreateRelease(fullV2, VersionTwo);
        using var client = new HttpClient();
        var state = new JsonInstallStateRepository(
            new JsonInstallStateRepositoryOptions(stateRoot));
        var journal = new JsonOperationJournalRepository(
            new JsonOperationJournalRepositoryOptions(journalRoot));
        var actualEngine = new VelopackPackageEngine(new SystemProcessRunner());

        try
        {
            var initial = new InstallerWorkflowService(
                new DefaultArtifactDownloader(client),
                new Sha256ArtifactVerifier(), actualEngine, state,
                operationJournal: journal);
            await initial.InstallAsync(
                original, installRoot, stageRoot, null, CancellationToken.None);
            CheckVersionAndData(marker, dataFile, VersionOne, expectedData: null);
            Directory.CreateDirectory(Path.GetDirectoryName(dataFile)!);
            await File.WriteAllTextAsync(dataFile, userBytes);

            // The child starts a real InstallerWorkflowService UpdateAsync
            // with real artifact verification, staging, and journal writes.
            // Before and after modes are deterministic state boundaries.
            // During mode terminates a verified, live Update.exe process;
            // it does not claim that a particular file write was interrupted.
            var assembly = typeof(Program).Assembly.Location;
            var start = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = root
            };
            foreach (var argument in new[]
            {
                assembly, "--crash-child", fullV2, installRoot,
                stageRoot, stateRoot, journalRoot, signal,
                mode.ToString(), versionMarker
            })
            {
                start.ArgumentList.Add(argument);
            }

            using var child = new Process { StartInfo = start };
            if (!child.Start())
            {
                throw new InvalidOperationException("Crash LAB child process failed to start.");
            }
            var output = child.StandardOutput.ReadToEndAsync();
            var errors = child.StandardError.ReadToEndAsync();
            try
            {
                var deadline = DateTimeOffset.UtcNow + SignalTimeout;
                while (!File.Exists(signal) &&
                    !child.HasExited &&
                    DateTimeOffset.UtcNow < deadline)
                {
                    await Task.Delay(100);
                }

                if (!File.Exists(signal))
                {
                    if (child.HasExited)
                    {
                        throw new InvalidOperationException(
                            "Crash LAB child exited before Applying checkpoint: " +
                            (await errors).TakeLastText(1600));
                    }
                    throw new TimeoutException(
                        "Crash LAB child never reached the requested updater boundary.");
                }
                var signalContent = await File.ReadAllTextAsync(signal);
                Process? liveUpdater = null;
                try
                {
                    if (mode == CrashBoundaryMode.DuringUpdater)
                    {
                        var prefix = SignalWhileRunningText + SignalSeparator;
                        if (!signalContent.StartsWith(prefix, StringComparison.Ordinal) ||
                            !int.TryParse(signalContent[prefix.Length..], out var updaterPid) ||
                            updaterPid <= 0)
                        {
                            throw new InvalidDataException(
                                "Crash LAB updater PID signal was invalid.");
                        }

                        liveUpdater = Process.GetProcessById(updaterPid);
                        if (liveUpdater.HasExited ||
                            !string.Equals(liveUpdater.ProcessName,
                                UpdaterExecutableName, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                "Crash LAB did not observe a running Update.exe.");
                        }
                    }
                    else if (!string.Equals(signalContent,
                        mode == CrashBoundaryMode.AfterUpdater
                            ? SignalAfterApplyText : SignalText,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Crash LAB signal was invalid.");
                    }

                var checkpoint = await journal.GetAsync(
                    packageId, CancellationToken.None);
                if (checkpoint?.Phase != InstallerOperationPhase.Applying ||
                    checkpoint.Operation != InstallerOperationType.Update)
                {
                    throw new InvalidOperationException(
                        "Updater launch signal lacked the durable Applying checkpoint.");
                }
                if (child.HasExited)
                {
                    throw new InvalidOperationException(
                        "Crash LAB child exited before process termination.");
                }

                child.Kill(entireProcessTree: true);
                using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                await child.WaitForExitAsync(wait.Token);
                if (!child.HasExited)
                {
                    throw new InvalidOperationException("Crash LAB child was not terminated.");
                }
                if (liveUpdater is not null)
                {
                    using var updaterWait = new CancellationTokenSource(
                        TimeSpan.FromSeconds(12));
                    await liveUpdater.WaitForExitAsync(updaterWait.Token);
                }
                await output;
                await errors;

                // The process dies before the journal can commit V2.
                // After-mode has actually applied V2 on disk; before-mode
                // has not launched Update.exe. Both retain committed V1.
                var expectedDiskVersion = mode == CrashBoundaryMode.AfterUpdater
                    ? VersionTwo : VersionOne;
                if (mode == CrashBoundaryMode.DuringUpdater)
                {
                    CheckUserData(dataFile, userBytes);
                }
                else
                {
                    CheckVersionAndData(marker, dataFile, expectedDiskVersion, userBytes);
                }
                var committed = await state.GetAsync(
                    packageId, CancellationToken.None)
                    ?? throw new InvalidOperationException(
                        "Crash LAB committed install state disappeared.");
                if (committed.Version.ToString() != VersionOne)
                {
                    throw new InvalidOperationException(
                        "Process termination unexpectedly committed the new version.");
                }

                var reopenedJournal = new JsonOperationJournalRepository(
                    new JsonOperationJournalRepositoryOptions(journalRoot));
                var reopenedState = new JsonInstallStateRepository(
                    new JsonInstallStateRepositoryOptions(stateRoot));

                var assessment = await new InstallerRecoveryAssessmentService(
                    reopenedState, reopenedJournal).AssessAsync(
                        packageId, CancellationToken.None);
                if (assessment.Status !=
                        InstallerRecoveryAssessmentStatus.RecoveryRequired ||
                    assessment.OperationId != checkpoint.OperationId ||
                    assessment.JournalPhase != InstallerOperationPhase.Applying ||
                    assessment.CommittedVersion != VersionOne ||
                    assessment.RequestedVersion != VersionTwo ||
                    !assessment.RequiresHumanReview)
                {
                    throw new InvalidOperationException(
                        "Read-only recovery assessment missed the orphaned Applying checkpoint.");
                }
                Console.WriteLine(
                    mode switch
                    {
                        CrashBoundaryMode.BeforeUpdater =>
                            "TURKUAZ_INSTALLER_PRE_APPLY_READ_ONLY_RECOVERY_REPORT_OK",
                        CrashBoundaryMode.AfterUpdater =>
                            "TURKUAZ_INSTALLER_POST_APPLY_READ_ONLY_RECOVERY_REPORT_OK",
                        _ => "TURKUAZ_INSTALLER_RUNNING_UPDATER_READ_ONLY_RECOVERY_REPORT_OK"
                    });

                var retry = new InstallerWorkflowService(
                    new DefaultArtifactDownloader(client),
                    new Sha256ArtifactVerifier(), actualEngine, reopenedState,
                    operationJournal: reopenedJournal);
                var blockedUpdate = false;
                try
                {
                    await retry.UpdateAsync(
                        update, committed, stageRoot, null, CancellationToken.None);
                }
                catch (InstallerRecoveryRequiredException)
                {
                    blockedUpdate = true;
                }
                if (!blockedUpdate)
                {
                    throw new InvalidOperationException(
                        "Post-crash update was not blocked.");
                }

                var blockedUninstall = false;
                try
                {
                    await retry.UninstallAsync(
                        committed, null, CancellationToken.None);
                }
                catch (InstallerRecoveryRequiredException)
                {
                    blockedUninstall = true;
                }
                if (!blockedUninstall)
                {
                    throw new InvalidOperationException(
                        "Post-crash uninstall was not blocked.");
                }

                var preserved = await reopenedJournal.GetAsync(
                    packageId, CancellationToken.None);
                if (preserved?.OperationId != checkpoint.OperationId ||
                    preserved.Phase != InstallerOperationPhase.Applying)
                {
                    throw new InvalidOperationException(
                        "Post-crash retry overwrote the orphaned journal.");
                }
                if (mode == CrashBoundaryMode.DuringUpdater)
                {
                    CheckUserData(dataFile, userBytes);
                    Console.WriteLine(
                        "TURKUAZ_INSTALLER_RUNNING_UPDATER_PROCESS_KILL_HOLD_OK");
                }
                else
                {
                    CheckVersionAndData(
                        marker, dataFile, expectedDiskVersion, userBytes);
                    Console.WriteLine(mode == CrashBoundaryMode.AfterUpdater
                        ? "TURKUAZ_INSTALLER_REAL_POST_UPDATER_KILL_DISK_STATE_DIVERGENCE_HOLD_OK"
                        : "TURKUAZ_INSTALLER_REAL_PROCESS_KILL_APPLY_CHECKPOINT_HOLD_OK");
                }
                }
                finally
                {
                    liveUpdater?.Dispose();
                }
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync();
                }
            }
        }
        finally
        {
            // Only bypass the guarded workflow to remove this synthetic
            // disposable test app; no customer recovery policy is changed.
            if (File.Exists(Path.Combine(installRoot, "Update.exe")))
            {
                try
                {
                    using var cleanupTimeout = new CancellationTokenSource(
                        TimeSpan.FromSeconds(75));
                    await actualEngine.UninstallAsync(
                        new UninstallPlan(packageId,
                            SemanticVersion.Parse(VersionOne), installRoot),
                        cleanupTimeout.Token);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(
                        "Crash LAB fixture cleanup warning: " + error.Message);
                }
            }
        }
    }

    public static async Task RunChildAsync(string[] args)
    {
        if (args.Length != 8 ||
            !OperatingSystem.IsWindows() ||
            !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
                "true", StringComparison.Ordinal) ||
            !string.Equals(Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT"),
                "github-hosted", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Crash LAB child is restricted to GitHub-hosted Windows.");
        }

        var release = CreateRelease(args[0], VersionTwo);
        var installRoot = Path.GetFullPath(args[1]);
        var state = new JsonInstallStateRepository(
            new JsonInstallStateRepositoryOptions(args[3]));
        var journal = new JsonOperationJournalRepository(
            new JsonOperationJournalRepositoryOptions(args[4]));
        using var client = new HttpClient();
        var service = new InstallerWorkflowService(
            new DefaultArtifactDownloader(client),
            new Sha256ArtifactVerifier(),
            new VelopackPackageEngine(new LabGatedUpdaterRunner(
                installRoot, args[5],
                mode: Enum.TryParse<CrashBoundaryMode>(
                    args[6], ignoreCase: false, out var parsedMode) &&
                    Enum.IsDefined(parsedMode)
                    ? parsedMode
                    : throw new InvalidOperationException(
                        "Unexpected LAB updater boundary."),
                versionMarker: args[7])),
            state,
            operationJournal: journal);
        var installed = await state.GetAsync(
            release.PackageId, CancellationToken.None)
            ?? throw new InvalidOperationException("Missing initial installed state.");

        await service.UpdateAsync(
            release, installed, args[2], null, CancellationToken.None);
        throw new InvalidOperationException(
            "Crash LAB child should have been terminated at the apply boundary.");
    }

    private static PackageRelease CreateRelease(string path, string version)
    {
        var file = Path.GetFullPath(path);
        using var stream = File.OpenRead(file);
        var digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var uri = new UriBuilder(Uri.UriSchemeFile, string.Empty)
        {
            Path = file
        }.Uri;
        return new PackageRelease(
            PackageId.Parse(PackageName),
            SemanticVersion.Parse(version), ReleaseChannel.Stable,
            new ArtifactDescriptor(
                uri, ArtifactDigest.ParseSha256(digest), stream.Length));
    }

    private static void CheckVersionAndData(
        string marker, string dataFile, string version, string? expectedData)
    {
        if (!File.Exists(marker) ||
            !string.Equals(File.ReadAllText(marker).Trim(), version,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Crash LAB installed version differs.");
        }
        if (expectedData is not null)
        {
            CheckUserData(dataFile, expectedData);
        }
    }

    private static void CheckUserData(string dataFile, string expectedData)
    {
        if (!File.Exists(dataFile) ||
            !string.Equals(File.ReadAllText(dataFile), expectedData,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Crash LAB user data changed.");
        }
    }

    private sealed class LabGatedUpdaterRunner : IProcessRunner
    {
        private readonly string _installRoot;
        private readonly string _signal;
        private readonly CrashBoundaryMode _mode;
        private readonly string _versionMarker;

        public LabGatedUpdaterRunner(
            string installRoot, string signal, CrashBoundaryMode mode,
            string versionMarker)
        {
            _installRoot = Path.GetFullPath(installRoot);
            _signal = Path.GetFullPath(signal);
            _mode = mode;
            if (string.IsNullOrWhiteSpace(versionMarker) ||
                Path.GetFileName(versionMarker) != versionMarker)
            {
                throw new InvalidOperationException(
                    "Crash LAB version marker must be a direct-child filename.");
            }
            _versionMarker = versionMarker;
        }

        public async Task<ProcessResult> RunAsync(
            ProcessCommand command, CancellationToken cancellationToken)
        {
            var allowedUpdater = Path.Combine(_installRoot, "Update.exe");
            if (!string.Equals(Path.GetFullPath(command.FileName), allowedUpdater,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Crash LAB may only gate its own fixture Update.exe.");
            }
            if (!command.Arguments.Contains("apply", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Crash LAB expected Velopack apply command.");
            }

            if (_mode == CrashBoundaryMode.DuringUpdater)
            {
                var start = new ProcessStartInfo
                {
                    FileName = command.FileName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = command.WorkingDirectory ?? _installRoot
                };
                foreach (var argument in command.Arguments)
                {
                    start.ArgumentList.Add(argument);
                }

                using var updater = new Process { StartInfo = start };
                if (!updater.Start() || updater.HasExited)
                {
                    throw new InvalidOperationException(
                        "Crash LAB Update.exe was not running after launch.");
                }

                // The parent will check the live PID and terminate the
                // workflow process tree. Never return to state commit even
                // if the updater wins the race and exits first.
                var stdout = updater.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderr = updater.StandardError.ReadToEndAsync(cancellationToken);
                await WriteSignalAsync(
                    SignalWhileRunningText + SignalSeparator + updater.Id,
                    cancellationToken);
                await updater.WaitForExitAsync(cancellationToken);
                await Task.WhenAll(stdout, stderr);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException(
                    "Crash LAB updater-running boundary unexpectedly returned.");
            }

            if (_mode == CrashBoundaryMode.AfterUpdater)
            {
                // The real, hosted-Windows Velopack updater replaces the
                // synthetic fixture before the child process is suspended.
                // The parent will kill the workflow between application and
                // the committed JSON V2 state.
                var result = await new SystemProcessRunner()
                    .RunAsync(command, cancellationToken);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Real LAB updater failed before post-apply gate: " +
                        result.StandardError.TakeLastText(1800));
                }

                var marker = Path.Combine(
                    _installRoot, "current", _versionMarker);
                if (!File.Exists(marker) ||
                    !string.Equals(File.ReadAllText(marker).Trim(),
                        VersionTwo, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Real LAB updater returned without installed V2 marker.");
                }
            }

            await WriteSignalAsync(
                _mode == CrashBoundaryMode.AfterUpdater
                    ? SignalAfterApplyText : SignalText,
                cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException(
                "Crash LAB gate must not return to the workflow.");
        }

        private async Task WriteSignalAsync(
            string signalText, CancellationToken cancellationToken)
        {
            var partialSignal = _signal + ".tmp";
            await File.WriteAllTextAsync(
                partialSignal, signalText, cancellationToken);
            File.Move(partialSignal, _signal, overwrite: true);
        }
    }
}

internal static class CrashLabTextExtensions
{
    public static string TakeLastText(this string value, int length)
        => value.Length > length ? value[^length..] : value;
}
