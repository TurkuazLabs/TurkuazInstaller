// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/InstalledAppCatalogServiceTests.cs
// 📌 Amac: Committed install state listesinin Presentation katalog satirlarina dogru map edilmesini test eder
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Package id sirasi, semantic version, localized channel label ve target path mappingini framework bagimsiz dogrular
//
// Bagimli Oldugu Katman: Service | Repo | View | Language

using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.Services;
using Xunit;

namespace TurkuazInstaller.Presentation.Tests;

public sealed class InstalledAppCatalogServiceTests
{
    [Fact]
    public async Task LoadAsync_MapsInstalledStatesInPackageOrder()
    {
        var repository =
            new StubStateRepository(
                new[]
                {
                    new InstalledPackageState(
                        PackageId.Parse(
                            "zeta-app"),
                        SemanticVersion.Parse(
                            "2.0.0-beta.1"),
                        ReleaseChannel.Beta,
                        "C:/Apps/Zeta"),
                    new InstalledPackageState(
                        PackageId.Parse(
                            "alpha-app"),
                        SemanticVersion.Parse(
                            "1.4.0"),
                        ReleaseChannel.Stable,
                        "C:/Apps/Alpha")
                });

        var service =
            new InstalledAppCatalogService(
                repository);

        var items =
            await service.LoadAsync(
                CancellationToken.None);

        Assert.Equal(
            2,
            items.Count);

        Assert.Equal(
            "alpha-app",
            items[0].PackageId);

        Assert.Equal(
            "1.4.0",
            items[0].Version);

        Assert.Equal(
            InstallerUiLabels.Stable,
            items[0].Channel);

        Assert.Equal(
            "C:/Apps/Alpha",
            items[0].TargetPath);

        Assert.Equal(
            InstallerUiLabels.Beta,
            items[1].Channel);
    }

    [Fact]
    public async Task LoadAsync_UsesLocalizedChannelLabels()
    {
        var repository =
            new StubStateRepository(
                new[]
                {
                    new InstalledPackageState(
                        PackageId.Parse(
                            "example-app"),
                        SemanticVersion.Parse(
                            "1.0.0"),
                        ReleaseChannel.Beta,
                        "C:/Apps/Example")
                });

        var catalog =
            new InstallerUiCatalog(
                new InstallerUiProfile(
                    "en-US",
                    InstallerBrandingProfile.Empty,
                    new Dictionary<InstallerUiLabelKey, string>
                    {
                        [InstallerUiLabelKey.Beta] =
                            "Preview"
                    }));

        var service =
            new InstalledAppCatalogService(
                repository,
                catalog);

        var item =
            Assert.Single(
                await service.LoadAsync(
                    CancellationToken.None));

        Assert.Equal(
            "Preview",
            item.Channel);
    }

    private sealed class StubStateRepository
        : IInstallStateRepository
    {
        private readonly IReadOnlyList<InstalledPackageState>
            _states;

        public StubStateRepository(
            IReadOnlyList<InstalledPackageState> states)
        {
            _states = states;
        }

        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<InstalledPackageState?>(
                _states.FirstOrDefault(
                    state =>
                        state.PackageId ==
                        packageId));
        }

        public Task<IReadOnlyList<InstalledPackageState>> ListAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _states);
        }

        public Task SaveAsync(
            InstalledPackageState state,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
