// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/services/BootstrapRuntimeService.cs
// 📌 Amac: Native bootstrap startup, prerequisite, cleanup ve self-update completion akislarini koordine eder
// 📌 Modul - Service CSharp
// Version: 0.6.0
// Aciklama: Controlleri ince tutar; is akisini Application prerequisite servisi ve Tool portlari uzerinden calistirir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Bootstrapper.Tools;
using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Bootstrapper.Services;

internal sealed class BootstrapRuntimeService
{
    private readonly BootstrapPrerequisiteService _prerequisiteService;
    private readonly ISelfUpdateHandoff _selfUpdateHandoff;
    private readonly IBootstrapFileCleaner _fileCleaner;

    public BootstrapRuntimeService(
        BootstrapPrerequisiteService prerequisiteService,
        ISelfUpdateHandoff selfUpdateHandoff,
        IBootstrapFileCleaner fileCleaner)
    {
        _prerequisiteService = prerequisiteService;
        _selfUpdateHandoff = selfUpdateHandoff;
        _fileCleaner = fileCleaner;
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

        var prerequisite = _prerequisiteService.Evaluate();

        if (prerequisite.IsSatisfied)
        {
            return BootstrapExitCode.Success;
        }

        return prerequisite.Failure switch
        {
            BootstrapPrerequisiteFailure.UnsupportedOperatingSystem =>
                BootstrapExitCode.UnsupportedOperatingSystem,
            BootstrapPrerequisiteFailure.UnsupportedWindowsVersion =>
                BootstrapExitCode.UnsupportedWindowsVersion,
            BootstrapPrerequisiteFailure.UnsupportedArchitecture =>
                BootstrapExitCode.UnsupportedArchitecture,
            _ => BootstrapExitCode.RuntimeFailure
        };
    }
}
