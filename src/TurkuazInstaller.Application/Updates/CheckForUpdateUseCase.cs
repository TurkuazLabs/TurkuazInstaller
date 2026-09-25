// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/CheckForUpdateUseCase.cs
// 📌 Amac: Kurulu surum ile release kaynagindaki son surumu karsilastirir
// 📌 Modul - Application CSharp
// Version: 0.1.0
// Aciklama: Update kararini provider ve UI detaylarindan bagimsiz olarak verir
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Updates;

public sealed class CheckForUpdateUseCase
{
    private readonly IReleaseSource _releaseSource;

    public CheckForUpdateUseCase(IReleaseSource releaseSource)
    {
        _releaseSource = releaseSource;
    }

    public async Task<CheckForUpdateResult> ExecuteAsync(
        ProductId productId,
        Version? installedVersion,
        ReleaseChannel channel,
        CpuArchitecture architecture,
        CancellationToken cancellationToken)
    {
        var latestRelease = await _releaseSource.GetLatestAsync(
            productId,
            channel,
            architecture,
            cancellationToken);

        if (latestRelease is null)
        {
            return new CheckForUpdateResult(UpdateAvailability.Unknown, null);
        }

        if (installedVersion is null)
        {
            return new CheckForUpdateResult(UpdateAvailability.NotInstalled, latestRelease);
        }

        var availability = latestRelease.Version > installedVersion
            ? UpdateAvailability.Available
            : UpdateAvailability.Current;

        return new CheckForUpdateResult(availability, latestRelease);
    }
}
