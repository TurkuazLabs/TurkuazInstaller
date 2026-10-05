// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/services/BootstrapRuntimeService.cs
// 📌 Amac: Native bootstrap startup, prerequisite, self-update ve desktop launch akislarini koordine eder
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Cleanup -> self-update handoff -> prerequisite -> combined distribution WinUI launch siralamasini Port/Tool uzerinden uygular
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
    private readonly IBootstrapFileCleaner _fileCleaner;
    private readonly IBootstrapProcessContext _processContext;
    private readonly IBootstrapApplicationLauncher _applicationLauncher;
    private readonly BootstrapRuntimeOptions _runtimeOptions;

    public BootstrapRuntimeService(
        BootstrapPrerequisiteService prerequisiteService,
        ISelfUpdateHandoff selfUpdateHandoff,
        IBootstrapFileCleaner fileCleaner,
        IBootstrapProcessContext processContext,
        IBootstrapApplicationLauncher applicationLauncher,
        BootstrapRuntimeOptions runtimeOptions)
    {
        _prerequisiteService = prerequisiteService;
        _selfUpdateHandoff = selfUpdateHandoff;
        _fileCleaner = fileCleaner;
        _processContext = processContext;
        _applicationLauncher = applicationLauncher;
        _runtimeOptions = runtimeOptions;
    }

    public async Task<BootstrapExitCode> ExecuteAsync(
        BootstrapInvocation invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invocation);

        if (invocation.IsSelfUpdateCompletion)
        {
            await _selfUpdateHandoff
                .CompleteAsync(
                    invocation.SelfUpdateRequest!,
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

        await _selfUpdateHandoff
            .BeginAsync(
                new SelfUpdateStartRequest(
                    currentExecutablePath,
                    Path.GetFullPath(
                        invocation.SelfUpdateReplacementPath!),
                    invocation.ApplicationArguments),
                cancellationToken)
            .ConfigureAwait(false);
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
