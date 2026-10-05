// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/JsonOperationJournalRepositoryTests.cs
// 📌 Amac: JSON operation journal repository atomic save/read/delete davranisini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.2.0
// Aciklama: Crash/reboot checkpointi, pending prerequisite roundtrip ve tamamlaninca silme davranisini dogrular
//
// Bagimli Oldugu Katman: Repo

using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Operations;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class JsonOperationJournalRepositoryTests
{
    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsCheckpoint()
    {
        var root =
            CreateRoot();

        try
        {
            var repository =
                new JsonOperationJournalRepository(
                    new JsonOperationJournalRepositoryOptions(
                        root));

            var packageId =
                PackageId.Parse(
                    "example-app");

            var started =
                DateTimeOffset.UtcNow;

            var entry =
                new InstallerOperationJournalEntry(
                    Guid.NewGuid(),
                    packageId,
                    InstallerOperationType.Update,
                    "2.0.0",
                    "C:/Apps/Example",
                    InstallerOperationPhase.AwaitingReboot,
                    started,
                    started.AddSeconds(3),
                    null,
                    "dotnet-desktop-runtime");

            await repository
                .SaveAsync(
                    entry,
                    CancellationToken.None);

            var loaded =
                await repository
                    .GetAsync(
                        packageId,
                        CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(
                entry.OperationId,
                loaded.OperationId);
            Assert.Equal(
                packageId,
                loaded.PackageId);
            Assert.Equal(
                InstallerOperationPhase.AwaitingReboot,
                loaded.Phase);
            Assert.Equal(
                "2.0.0",
                loaded.Version);
            Assert.Equal(
                "dotnet-desktop-runtime",
                loaded.PendingPrerequisiteId);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesCheckpoint()
    {
        var root =
            CreateRoot();

        try
        {
            var repository =
                new JsonOperationJournalRepository(
                    new JsonOperationJournalRepositoryOptions(
                        root));

            var packageId =
                PackageId.Parse(
                    "example-app");

            var now =
                DateTimeOffset.UtcNow;

            await repository
                .SaveAsync(
                    new InstallerOperationJournalEntry(
                        Guid.NewGuid(),
                        packageId,
                        InstallerOperationType.Install,
                        "1.0.0",
                        "C:/Apps/Example",
                        InstallerOperationPhase.Started,
                        now,
                        now,
                        null),
                    CancellationToken.None);

            await repository
                .DeleteAsync(
                    packageId,
                    CancellationToken.None);

            Assert.Null(
                await repository
                    .GetAsync(
                        packageId,
                        CancellationToken.None));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static string CreateRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "TurkuazInstallerJournalTests",
            Guid.NewGuid()
                .ToString("N"));
    }

    private static void DeleteRoot(
        string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }
}
