// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/services/BootstrapRuntimeService.cs
// 📌 Amac: Native bootstrap startup, prerequisite, self-update ve desktop launch akislarini koordine eder
// 📌 Modul - Service CSharp
// Version: 1.2.0
// Aciklama: Cleanup -> trusted explicit/discovered self-update -> prerequisite -> combined distribution WinUI launch siralamasini Port/Tool uzerinden uygular
//
// Bagimli Oldugu Katman: Service | Tool | Config

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Bootstrapper.Config;
using TurkuazInstaller.Bootstrapper.Tools;
using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Bootstrapper.Services;

internal sealed class BootstrapRuntimeService
{
    private readonly BootstrapPrerequisiteService _prerequisiteService;
    private readonly ISelfUpdateHandoff _selfUpdateHandoff;
    private readonly IBootstrapSelfUpdateDiscovery _selfUpdateDiscovery;
    private readonly IBootstrapSelfUpdateDownloader _selfUpdateDownloader;
    private readonly IBootstrapSelfUpdateTrustVerifier _selfUpdateTrustVerifier;
    private readonly IBootstrapFileCleaner _fileCleaner;
    private readonly IBootstrapProcessContext _processContext;
    private readonly IBootstrapApplicationLauncher _applicationLauncher;
    private readonly BootstrapRuntimeOptions _runtimeOptions;
    private readonly BootstrapSelfUpdateOptions _selfUpdateOptions;

    public BootstrapRuntimeService(
        BootstrapPrerequisiteService prerequisiteService,
        ISelfUpdateHandoff selfUpdateHandoff,
        IBootstrapSelfUpdateDiscovery selfUpdateDiscovery,
        IBootstrapSelfUpdateDownloader selfUpdateDownloader,
        IBootstrapSelfUpdateTrustVerifier selfUpdateTrustVerifier,
        IBootstrapFileCleaner fileCleaner,
        IBootstrapProcessContext processContext,
        IBootstrapApplicationLauncher applicationLauncher,
        BootstrapRuntimeOptions runtimeOptions,
        BootstrapSelfUpdateOptions selfUpdateOptions)
    {
        _prerequisiteService = prerequisiteService;
        _selfUpdateHandoff = selfUpdateHandoff;
        _selfUpdateDiscovery = selfUpdateDiscovery;
        _selfUpdateDownloader = selfUpdateDownloader;
        _selfUpdateTrustVerifier = selfUpdateTrustVerifier;
        _fileCleaner = fileCleaner;
        _processContext = processContext;
        _applicationLauncher = applicationLauncher;
        _runtimeOptions = runtimeOptions;
        _selfUpdateOptions = selfUpdateOptions;
    }

    public async Task<BootstrapExitCode> ExecuteAsync(
        BootstrapInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (invocation.IsSelfUpdateCompletion)
        {
            var completion =
                invocation.SelfUpdateRequest!;

            await VerifyReplacementAsync(
                    completion.TargetExecutablePath,
                    completion.SourceExecutablePath,
                    cancellationToken)
                .ConfigureAwait(false);

            await _selfUpdateHandoff
                .CompleteAsync(
                    completion,
                    cancellationToken)
                .ConfigureAwait(false);

            return BootstrapExitCode.Success;
        }

        if (invocation.CleanupSourcePath is not null)
        {
            await _fileCleaner
                .TryDeleteAsync(
                    invocation.CleanupSourcePath,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (invocation.IsSelfUpdateStart)
        {
            await StartSelfUpdateAsync(
                    invocation,
                    cancellationToken)
                .ConfigureAwait(false);

            return BootstrapExitCode.Success;
        }

        var prerequisite =
            _prerequisiteService.Evaluate();

        if (!prerequisite.IsSatisfied)
        {
            return MapPrerequisiteFailure(
                prerequisite.Failure);
        }

        if (
            await TryStartDiscoveredSelfUpdateAsync(
                    invocation.ApplicationArguments,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return BootstrapExitCode.Success;
        }

        var desktopExecutablePath =
            ResolveDesktopExecutablePath();

        await _applicationLauncher
            .LaunchAsync(
                desktopExecutablePath,
                invocation.ApplicationArguments,
                cancellationToken)
            .ConfigureAwait(false);

        return BootstrapExitCode.Success;
    }

    private async Task StartSelfUpdateAsync(
        BootstrapInvocation invocation,
        CancellationToken cancellationToken)
    {
        var currentExecutablePath =
            _processContext
                .GetCurrentExecutablePath();

        var replacementExecutablePath =
            Path.GetFullPath(
                invocation.SelfUpdateReplacementPath!);

        await VerifyReplacementAsync(
                currentExecutablePath,
                replacementExecutablePath,
                cancellationToken)
            .ConfigureAwait(false);

        await _selfUpdateHandoff
            .BeginAsync(
                new SelfUpdateStartRequest(
                    currentExecutablePath,
                    replacementExecutablePath,
                    invocation.ApplicationArguments),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TryStartDiscoveredSelfUpdateAsync(
        IReadOnlyList<string> resumeArguments,
        CancellationToken cancellationToken)
    {
        var currentExecutablePath =
            _processContext
                .GetCurrentExecutablePath();

        if (
            !await _selfUpdateTrustVerifier
                .CanSelfUpdateAsync(
                    currentExecutablePath,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return false;
        }

        var release =
            await _selfUpdateDiscovery
                .GetLatestAsync(
                    _selfUpdateOptions.CurrentVersion,
                    cancellationToken)
                .ConfigureAwait(false);

        if (release is null)
        {
            return false;
        }

        var replacementExecutablePath =
            await _selfUpdateDownloader
                .DownloadAsync(
                    release,
                    _selfUpdateOptions.StagingRoot,
                    cancellationToken)
                .ConfigureAwait(false);

        var keepReplacement = false;

        try
        {
            await VerifyReplacementAsync(
                    currentExecutablePath,
                    replacementExecutablePath,
                    cancellationToken)
                .ConfigureAwait(false);

            await _selfUpdateHandoff
                .BeginAsync(
                    new SelfUpdateStartRequest(
                        currentExecutablePath,
                        replacementExecutablePath,
                        resumeArguments),
                    cancellationToken)
                .ConfigureAwait(false);

            keepReplacement = true;
            return true;
        }
        finally
        {
            if (!keepReplacement)
            {
                await _fileCleaner
                    .TryDeleteAsync(
                        replacementExecutablePath,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task VerifyReplacementAsync(
        string currentExecutablePath,
        string replacementExecutablePath,
        CancellationToken cancellationToken)
    {
        var verification =
            await _selfUpdateTrustVerifier
                .VerifyReplacementAsync(
                    currentExecutablePath,
                    replacementExecutablePath,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!verification.IsValid)
        {
            throw new InvalidDataException(
                string.Concat(
                    "Bootstrap self-update replacement trust verification failed: ",
                    verification.Message));
        }
    }

    private string ResolveDesktopExecutablePath()
    {
        var bootstrapPath =
            _processContext
                .GetCurrentExecutablePath();

        var root =
            Path.GetDirectoryName(
                bootstrapPath)
            ?? throw new InvalidOperationException(
                "Bootstrap distribution root could not be resolved.");

        var fullRoot =
            Path.GetFullPath(root);

        var candidate =
            Path.GetFullPath(
                Path.Combine(
                    fullRoot,
                    _runtimeOptions.ApplicationRelativePath));

        if (!IsPathWithinRoot(fullRoot, candidate))
        {
            throw new InvalidOperationException(
                "Bootstrap desktop application path escaped the distribution root.");
        }

        return candidate;
    }

    private static BootstrapExitCode MapPrerequisiteFailure(
        BootstrapPrerequisiteFailure failure)
    {
        return failure switch
        {
            BootstrapPrerequisiteFailure.UnsupportedOperatingSystem =>
                BootstrapExitCode.UnsupportedOperatingSystem,
            BootstrapPrerequisiteFailure.UnsupportedWindowsVersion =>
                BootstrapExitCode.UnsupportedWindowsVersion,
            BootstrapPrerequisiteFailure.UnsupportedArchitecture =>
                BootstrapExitCode.UnsupportedArchitecture,
            _ =>
                BootstrapExitCode.RuntimeFailure
        };
    }

    private static bool IsPathWithinRoot(
        string rootPath,
        string candidatePath)
    {
        var root =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(rootPath));

        var candidate =
            Path.GetFullPath(candidatePath);

        if (
            string.Equals(
                root,
                candidate,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator =
            string.Concat(
                root,
                Path.DirectorySeparatorChar);

        return candidate.StartsWith(
            rootWithSeparator,
            StringComparison.OrdinalIgnoreCase);
    }
}
