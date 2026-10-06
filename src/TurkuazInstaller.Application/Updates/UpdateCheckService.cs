// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/UpdateCheckService.cs
// 📌 Amac: Kurulu surum ile provider latest release bilgisini karsilastirarak update kararini verir
// 📌 Modul - Service CSharp
// Version: 1.1.0
// Aciklama: Provider latest signed release ile installed state'i karsilastirir; installed update varsa version skip/pinning policy uygular
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Updates;

public sealed class UpdateCheckService
{
    private readonly IReleaseProvider _releaseProvider;
    private readonly IInstallStateRepository _stateRepository;
    private readonly VersionUpdatePolicyService? _versionPolicyService;

    public UpdateCheckService(
        IReleaseProvider releaseProvider,
        IInstallStateRepository stateRepository,
        IVersionUpdatePolicyRepository? versionPolicyRepository = null)
    {
        _releaseProvider = releaseProvider;
        _stateRepository = stateRepository;

        _versionPolicyService =
            versionPolicyRepository is null
                ? null
                : new VersionUpdatePolicyService(
                    versionPolicyRepository);
    }

    public async Task<UpdateCheckResult> ExecuteAsync(PackageId packageId, ReleaseChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);

        var installed = await _stateRepository.GetAsync(packageId, cancellationToken).ConfigureAwait(false);
        var latest = await _releaseProvider.GetLatestReleaseAsync(packageId, channel, cancellationToken).ConfigureAwait(false);

        if (latest is null)
        {
            return new UpdateCheckResult(
                UpdateAvailability.ReleaseNotFound,
                null,
                installed);
        }

        if (installed is null)
        {
            return new UpdateCheckResult(
                UpdateAvailability.Available,
                latest,
                null);
        }

        if (latest.Version <= installed.Version)
        {
            return new UpdateCheckResult(
                UpdateAvailability.Current,
                latest,
                installed);
        }

        if (_versionPolicyService is not null)
        {
            var evaluation =
                await _versionPolicyService
                    .EvaluateAsync(
                        packageId,
                        channel,
                        latest.Version,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (
                evaluation.Decision ==
                VersionUpdatePolicyDecision.Skipped)
            {
                return new UpdateCheckResult(
                    UpdateAvailability.Skipped,
                    latest,
                    installed);
            }

            if (
                evaluation.Decision ==
                VersionUpdatePolicyDecision.Pinned)
            {
                return new UpdateCheckResult(
                    UpdateAvailability.Pinned,
                    latest,
                    installed);
            }
        }

        return new UpdateCheckResult(
            UpdateAvailability.Available,
            latest,
            installed);
    }
}
