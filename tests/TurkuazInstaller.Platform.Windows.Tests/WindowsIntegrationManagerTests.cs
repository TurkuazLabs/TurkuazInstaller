// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsIntegrationManagerTests.cs
// 📌 Amac: Windows integration manager receipt reconcile ve uninstall cleanup davranisini deterministik seam'lerle test eder
// 📌 Modul - Test CSharp
// Version: 1.2.0
// Aciklama: Reconcile/remove davranisina ek olarak cleanup failure sonrasi ownership receipt'inin korundugunu dogrular
//
// Bagimli Oldugu Katman: Tool | Repo | Service

using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsIntegrationManagerTests
{
    private const string HashA =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string HashB =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task ApplyAsync_Reconcile_RemovesStaleOwnedActionsAndSavesDesiredReceipt()
    {
        var root =
            CreateTempRoot();

        try
        {
            var packageId =
                PackageId.Parse(
                    "example-app");

            var receiptStore =
                new WindowsIntegrationReceiptStore(
                    root);

            await receiptStore.SaveAsync(
                new WindowsIntegrationReceipt(
                    packageId,
                    new[]
                    {
                        new WindowsShortcutReceipt(
                            "old",
                            Path.Combine(
                                root,
                                "Old.lnk"),
                            HashA)
                    },
                    new[]
                    {
                        "old-protocol"
                    }),
                CancellationToken.None);

            var shortcutStore =
                new StubShortcutStore(
                    root);

            var protocolStore =
                new StubProtocolStore();

            var manager =
                new WindowsIntegrationManager(
                    receiptStore,
                    shortcutStore,
                    protocolStore);

            var policy =
                new WindowsIntegrationPolicy(
                    new[]
                    {
                        new WindowsShortcutIntegration(
                            "main",
                            "Example App",
                            WindowsShortcutLocation.StartMenu,
                            "ExampleApp.exe")
                    },
                    new[]
                    {
                        new WindowsProtocolIntegration(
                            "example-app",
                            "ExampleApp.exe")
                    });

            await manager.ApplyAsync(
                packageId,
                "C:/Apps/Example",
                policy,
                CancellationToken.None);

            Assert.Single(
                shortcutStore.Removed);

            Assert.Equal(
                "old",
                shortcutStore.Removed[0]
                    .ActionId);

            Assert.Contains(
                "old-protocol",
                protocolStore.Removed,
                StringComparer.OrdinalIgnoreCase);

            var receipt =
                await receiptStore.GetAsync(
                    packageId,
                    CancellationToken.None);

            Assert.NotNull(
                receipt);

            var shortcut =
                Assert.Single(
                    receipt!.Shortcuts);

            Assert.Equal(
                "main",
                shortcut.ActionId);

            Assert.Equal(
                HashB,
                shortcut.Sha256);

            Assert.Equal(
                "example-app",
                Assert.Single(
                    receipt.ProtocolSchemes));
        }
        finally
        {
            DeleteTempRoot(
                root);
        }
    }

    [Fact]
    public async Task RemoveAsync_CleanupFailure_PreservesReceiptForRetry()
    {
        var root =
            CreateTempRoot();

        try
        {
            var packageId =
                PackageId.Parse(
                    "example-app");

            var receiptStore =
                new WindowsIntegrationReceiptStore(
                    root);

            await receiptStore.SaveAsync(
                new WindowsIntegrationReceipt(
                    packageId,
                    new[]
                    {
                        new WindowsShortcutReceipt(
                            "main",
                            Path.Combine(
                                root,
                                "Example.lnk"),
                            HashA)
                    },
                    new[]
                    {
                        "example-app"
                    }),
                CancellationToken.None);

            var shortcutStore =
                new StubShortcutStore(
                    root)
                {
                    RemoveException =
                        new IOException(
                            "Simulated shortcut cleanup failure.")
                };

            var manager =
                new WindowsIntegrationManager(
                    receiptStore,
                    shortcutStore,
                    new StubProtocolStore());

            await Assert.ThrowsAsync<AggregateException>(
                () =>
                    manager.RemoveAsync(
                        packageId,
                        CancellationToken.None));

            Assert.NotNull(
                await receiptStore.GetAsync(
                    packageId,
                    CancellationToken.None));
        }
        finally
        {
            DeleteTempRoot(
                root);
        }
    }

    [Fact]
    public async Task RemoveAsync_CleansOwnedActionsAndDeletesReceipt()
    {
        var root =
            CreateTempRoot();

        try
        {
            var packageId =
                PackageId.Parse(
                    "example-app");

            var receiptStore =
                new WindowsIntegrationReceiptStore(
                    root);

            var shortcutReceipt =
                new WindowsShortcutReceipt(
                    "main",
                    Path.Combine(
                        root,
                        "Example.lnk"),
                    HashA);

            await receiptStore.SaveAsync(
                new WindowsIntegrationReceipt(
                    packageId,
                    new[]
                    {
                        shortcutReceipt
                    },
                    new[]
                    {
                        "example-app"
                    }),
                CancellationToken.None);

            var shortcutStore =
                new StubShortcutStore(
                    root);

            var protocolStore =
                new StubProtocolStore();

            var manager =
                new WindowsIntegrationManager(
                    receiptStore,
                    shortcutStore,
                    protocolStore);

            await manager.RemoveAsync(
                packageId,
                CancellationToken.None);

            Assert.Single(
                shortcutStore.Removed);

            Assert.Equal(
                "example-app",
                Assert.Single(
                    protocolStore.Removed));

            Assert.Null(
                await receiptStore.GetAsync(
                    packageId,
                    CancellationToken.None));
        }
        finally
        {
            DeleteTempRoot(
                root);
        }
    }

    private static string CreateTempRoot()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.IntegrationTests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            root);

        return root;
    }

    private static void DeleteTempRoot(
        string root)
    {
        if (Directory.Exists(
                root))
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    private sealed class StubShortcutStore
        : IWindowsShortcutStore
    {
        private readonly string _root;

        public StubShortcutStore(
            string root)
        {
            _root = root;
        }

        public List<WindowsShortcutReceipt> Removed
        {
            get;
        } = new();

        public Exception? RemoveException
        {
            get;
            init;
        }

        public Task<WindowsShortcutReceipt> CreateAsync(
            PackageId packageId,
            string targetPath,
            WindowsShortcutIntegration action,
            WindowsShortcutReceipt? existingReceipt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new WindowsShortcutReceipt(
                    action.Id,
                    Path.Combine(
                        _root,
                        string.Concat(
                            action.Id,
                            ".lnk")),
                    HashB));
        }

        public Task RemoveOwnedAsync(
            PackageId packageId,
            WindowsShortcutReceipt receipt,
            CancellationToken cancellationToken)
        {
            Removed.Add(
                receipt);

            if (RemoveException is not null)
            {
                throw RemoveException;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubProtocolStore
        : IWindowsProtocolRegistrationStore
    {
        public List<string> Applied
        {
            get;
        } = new();

        public List<string> Removed
        {
            get;
        } = new();

        public Task ApplyAsync(
            PackageId packageId,
            string targetPath,
            WindowsProtocolIntegration action,
            CancellationToken cancellationToken)
        {
            Applied.Add(
                action.Scheme);

            return Task.CompletedTask;
        }

        public Task RemoveOwnedAsync(
            PackageId packageId,
            string scheme,
            CancellationToken cancellationToken)
        {
            Removed.Add(
                scheme);

            return Task.CompletedTask;
        }
    }
}
