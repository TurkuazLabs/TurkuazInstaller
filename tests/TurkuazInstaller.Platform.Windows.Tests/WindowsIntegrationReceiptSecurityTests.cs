// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsIntegrationReceiptSecurityTests.cs
// 📌 Amac: Bozuk Windows integration receipt verisinin shortcut/protocol cleanup sinirlarini asamadigini test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Gecersiz protocol scheme ve package-owned lokasyon disindaki shortcut receipt path degerlerini fail-closed dogrular
//
// Bagimli Oldugu Katman: Repo | Tool | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsIntegrationReceiptSecurityTests
{
    private const string Hash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task GetAsync_InvalidProtocolSchemeInReceipt_Rejects()
    {
        var root =
            CreateTempRoot();

        try
        {
            var packageId =
                PackageId.Parse(
                    "example-app");

            var receiptPath =
                Path.Combine(
                    root,
                    string.Concat(
                        packageId.Value,
                        ".json"));

            await File.WriteAllTextAsync(
                receiptPath,
                """
{
  "PackageId": "example-app",
  "Shortcuts": [],
  "ProtocolSchemes": [
    "1invalid"
  ]
}
""");

            var store =
                new WindowsIntegrationReceiptStore(
                    root);

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    store.GetAsync(
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
    public async Task RemoveOwnedAsync_ShortcutPathOutsideOwnedLocations_Rejects()
    {
        var root =
            CreateTempRoot();

        try
        {
            var receipt =
                new WindowsShortcutReceipt(
                    "main",
                    Path.Combine(
                        root,
                        "Outside.lnk"),
                    Hash);

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    new WindowsShellLinkShortcutStore()
                        .RemoveOwnedAsync(
                            PackageId.Parse(
                                "example-app"),
                            receipt,
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
                "TurkuazInstaller.IntegrationReceiptSecurityTests",
                Guid.NewGuid()
                    .ToString("N"));

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
}
