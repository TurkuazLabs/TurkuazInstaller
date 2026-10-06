// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/UpdateCheckService.cs
// 📌 Amac: Kurulu surum ile provider latest release bilgisini karsilastirarak update kararini verir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Provider latest signed release ile installed state bilgisini read-only karsilastirir ve ikisini typed sonuc modelinde dondurur
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Updates;

public sealed class UpdateCheckService
{
    private readonly IReleaseProvider _releaseProvider;
    private readonly IInstallStateRepository _stateRepository;

    public UpdateCheckService(IReleaseProvider releaseProvider, IInstallStateRepository stateRepository)
    {
        _releaseProvider = releaseProvider;
        _stateRepository = stateRepository;
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

        if (
            installed is null ||
            latest.Version > installed.Version)
        {
            return new UpdateCheckResult(
                UpdateAvailability.Available,
                latest,
                installed);
        }

        return new UpdateCheckResult(
            UpdateAvailability.Current,
            latest,
            installed);
    }
}
