// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/InstalledAppCatalogService.cs
// 📌 Amac: Committed install state listesini kullaniciya gosterilecek kurulu uygulama katalog modeline donusturur
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Repository ListAsync sonucunu deterministic salt-okunur ViewModel satirlarina map eder
//
// Bagimli Oldugu Katman: Service | Repo | View | Language

using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.ViewModels;

namespace TurkuazInstaller.Presentation.Services;

public sealed class InstalledAppCatalogService
{
    private readonly IInstallStateRepository
        _stateRepository;

    public InstalledAppCatalogService(
        IInstallStateRepository stateRepository)
    {
        ArgumentNullException.ThrowIfNull(
            stateRepository);

        _stateRepository =
            stateRepository;
    }

    public async Task<IReadOnlyList<InstalledAppListItemViewModel>>
        LoadAsync(
            CancellationToken cancellationToken)
    {
        var states =
            await _stateRepository
                .ListAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return states
            .OrderBy(
                state =>
                    state.PackageId.Value,
                StringComparer.Ordinal)
            .Select(
                state =>
                    new InstalledAppListItemViewModel(
                        state.PackageId.Value,
                        state.Version.ToString(),
                        state.Channel ==
                            ReleaseChannel.Beta
                            ? InstallerUiLabels.Beta
                            : InstallerUiLabels.Stable,
                        state.TargetPath))
            .ToArray();
    }
}
