// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/UpdateCheckService.cs
// 📌 Amac: Kurulu surum ile provider latest release bilgisini karsilastirarak update kararini verir
// 📌 Modul - Service CSharp
// Version: 0.3.0
// Aciklama: Provider ve storage detaylarini portlar arkasinda tutan Application servisidir
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

        if (latest is null) return new UpdateCheckResult(UpdateAvailability.ReleaseNotFound, null);
        if (installed is null || latest.Version > installed.Version) return new UpdateCheckResult(UpdateAvailability.Available, latest);
        return new UpdateCheckResult(UpdateAvailability.Current, latest);
    }
}
