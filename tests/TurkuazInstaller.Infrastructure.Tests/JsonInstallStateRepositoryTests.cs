// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/JsonInstallStateRepositoryTests.cs
// 📌 Amac: JSON install state repository atomic save ve roundtrip davranisini gercek dosya sistemiyle test eder
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Atomic roundtrip/delete davranisina ek olarak committed state listesinin deterministic package id sirasini dogrular
//
// Bagimli Oldugu Katman: Repo

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Infrastructure.Repositories;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class JsonInstallStateRepositoryTests
{
    [Fact]
    public async Task DeleteAsync_RemovesCommittedState()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "TurkuazInstallerStateTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var repository =
                new JsonInstallStateRepository(
                    new JsonInstallStateRepositoryOptions(
                        root));

            var packageId =
                PackageId.Parse("example-app");

            await repository.SaveAsync(
                new InstalledPackageState(
                    packageId,
                    SemanticVersion.Parse("1.0.0"),
                    ReleaseChannel.Stable,
                    "C:/Apps/Example"),
                CancellationToken.None);

            await repository.DeleteAsync(
                packageId,
                CancellationToken.None);

            var loaded =
                await repository.GetAsync(
                    packageId,
                    CancellationToken.None);

            Assert.Null(loaded);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }


    [Fact]
    public async Task ListAsync_ReturnsCommittedStatesInPackageIdOrder()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "TurkuazInstallerStateTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var repository =
                new JsonInstallStateRepository(
                    new JsonInstallStateRepositoryOptions(
                        root));

            await repository.SaveAsync(
                new InstalledPackageState(
                    PackageId.Parse(
                        "zeta-app"),
                    SemanticVersion.Parse(
                        "2.0.0"),
                    ReleaseChannel.Beta,
                    "C:/Apps/Zeta"),
                CancellationToken.None);

            await repository.SaveAsync(
                new InstalledPackageState(
                    PackageId.Parse(
                        "alpha-app"),
                    SemanticVersion.Parse(
                        "1.0.0"),
                    ReleaseChannel.Stable,
                    "C:/Apps/Alpha"),
                CancellationToken.None);

            var states =
                await repository.ListAsync(
                    CancellationToken.None);

            Assert.Equal(
                new[]
                {
                    "alpha-app",
                    "zeta-app"
                },
                states
                    .Select(
                        state =>
                            state.PackageId.Value)
                    .ToArray());

            Assert.Equal(
                "1.0.0",
                states[0]
                    .Version
                    .ToString());

            Assert.Equal(
                ReleaseChannel.Beta,
                states[1].Channel);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsLatestStateWithoutTempFile()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "TurkuazInstallerStateTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            var repository =
                new JsonInstallStateRepository(
                    new JsonInstallStateRepositoryOptions(
                        root));

            var packageId =
                PackageId.Parse("example-app");

            await repository.SaveAsync(
                new InstalledPackageState(
                    packageId,
                    SemanticVersion.Parse("1.0.0"),
                    ReleaseChannel.Stable,
                    "C:/Apps/Example"),
                CancellationToken.None);

            await repository.SaveAsync(
                new InstalledPackageState(
                    packageId,
                    SemanticVersion.Parse("1.1.0"),
                    ReleaseChannel.Stable,
                    "C:/Apps/Example"),
                CancellationToken.None);

            var loaded = await repository.GetAsync(
                packageId,
                CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(
                "1.1.0",
                loaded.Version.ToString());

            Assert.Empty(
                Directory.GetFiles(
                    root,
                    "*.tmp",
                    SearchOption.TopDirectoryOnly));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
