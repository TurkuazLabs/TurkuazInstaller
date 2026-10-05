// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/FileInstallerOperationLockTests.cs
// 📌 Amac: FileInstallerOperationLock cross-process exclusive lease davranisini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Ayni package icin ikinci lease'in reddedildigini ve ilk lease release edilince yeniden acquire edilebildigini dogrular
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Operations;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class FileInstallerOperationLockTests
{
    [Fact]
    public async Task AcquireAsync_SecondLeaseForSamePackage_IsRejectedUntilRelease()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstallerLockTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var operationLock =
                new FileInstallerOperationLock(
                    new FileInstallerOperationLockOptions(
                        root));

            var packageId =
                PackageId.Parse(
                    "example-app");

            var firstLease =
                await operationLock
                    .AcquireAsync(
                        packageId,
                        CancellationToken.None);

            await Assert.ThrowsAsync<PackageOperationLockedException>(
                () =>
                    operationLock.AcquireAsync(
                        packageId,
                        CancellationToken.None));

            await firstLease
                .DisposeAsync();

            await using var secondLease =
                await operationLock
                    .AcquireAsync(
                        packageId,
                        CancellationToken.None);
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
    public async Task AcquireAsync_DifferentPackages_CanRunConcurrently()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstallerLockTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var operationLock =
                new FileInstallerOperationLock(
                    new FileInstallerOperationLockOptions(
                        root));

            await using var firstLease =
                await operationLock
                    .AcquireAsync(
                        PackageId.Parse(
                            "example-app"),
                        CancellationToken.None);

            await using var secondLease =
                await operationLock
                    .AcquireAsync(
                        PackageId.Parse(
                            "another-app"),
                        CancellationToken.None);
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
