// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/InstalledAppCatalogService.cs
// 📌 Amac: Committed install state listesini kullaniciya gosterilecek kurulu uygulama katalog modeline donusturur
// 📌 Modul - Service CSharp
// Version: 1.1.0
// Aciklama: Repository ListAsync sonucunu deterministic ViewModel satirlarina map eder ve channel metnini typed Language katalogundan cozer
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

    private readonly InstallerUiCatalog
        _ui;

    public InstalledAppCatalogService(
        IInstallStateRepository stateRepository,
        InstallerUiCatalog? uiCatalog = null)
    {
        ArgumentNullException.ThrowIfNull(
            stateRepository);

        _stateRepository =
            stateRepository;

        _ui =
            uiCatalog
            ?? new InstallerUiCatalog(
                TurkuazInstaller.Contracts.Branding
                    .InstallerUiProfile.Empty);
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
                            ? _ui.Resolve(
                                TurkuazInstaller.Contracts.Branding
                                    .InstallerUiLabelKey.Beta)
                            : _ui.Resolve(
                                TurkuazInstaller.Contracts.Branding
                                    .InstallerUiLabelKey.Stable),
                        state.TargetPath))
            .ToArray();
    }
}
