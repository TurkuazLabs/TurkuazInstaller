// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/InstallerPayloadEvidenceTests.cs
// 📌 Amac: Gercek dosya hash olcumu, uyusmazlik, traversal ve salt-okunur davranisi test eder
// 📌 Modul - Test CSharp
// Version: 1.0.1
// Aciklama: Yalnizca LAB dizinlerinde hash olcer, onarim veya journal mutation yapmaz
// Bagimli Oldugu Katman: Service | Tool | Domain

using System.Security.Cryptography;
using System.Text;
using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Tools;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class InstallerPayloadEvidenceTests
{
    private static readonly PackageId Package = PackageId.Parse("payload-evidence-fixture");

    [Fact]
    public async Task AssessAsync_ExistingFileHashMatchesWithoutChangingDiskBytes()
    {
        var root = CreateLabRoot();
        try
        {
            var current = Path.Combine(root, "current");
            Directory.CreateDirectory(current);
            var marker = Path.Combine(current, "version.txt");
            var userData = Path.Combine(root, "UserData", "preferences.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(userData)!);
            await File.WriteAllTextAsync(marker, "2.0.0");
            await File.WriteAllTextAsync(userData, "KEEP_USER_DATA");
            var markerBefore = await File.ReadAllBytesAsync(marker);
            var userBefore = await File.ReadAllBytesAsync(userData);

            var assessment = await Create().AssessAsync(
                Package, root,
                new[] { Expect("current/version.txt", markerBefore) },
                CancellationToken.None);

            var result = Assert.Single(assessment.Files);
            Assert.Equal(InstallerPayloadFileStatus.MatchesSuppliedDigest, result.Status);
            Assert.Equal(Digest(markerBefore).Sha256, result.ActualSha256);
            Assert.Equal(markerBefore, await File.ReadAllBytesAsync(marker));
            Assert.Equal(userBefore, await File.ReadAllBytesAsync(userData));
        }
        finally
        {
            DeleteLabRoot(root);
        }
    }

    [Fact]
    public async Task AssessAsync_ReportsMismatchAndMissingFileWithoutMutation()
    {
        var root = CreateLabRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "current"));
            var marker = Path.Combine(root, "current", "version.txt");
            await File.WriteAllTextAsync(marker, "V2");
            var actualBefore = await File.ReadAllBytesAsync(marker);

            var assessment = await Create().AssessAsync(
                Package, root,
                new[]
                {
                    Expect("current/version.txt", Encoding.UTF8.GetBytes("V1")),
                    Expect("current/absent.txt", Encoding.UTF8.GetBytes("missing"))
                },
                CancellationToken.None);

            Assert.Equal(InstallerPayloadFileStatus.DigestMismatch,
                assessment.Files[0].Status);
            Assert.NotNull(assessment.Files[0].ActualSha256);
            Assert.Equal(InstallerPayloadFileStatus.Missing,
                assessment.Files[1].Status);
            Assert.Null(assessment.Files[1].ActualSha256);
            Assert.Equal(actualBefore, await File.ReadAllBytesAsync(marker));
            Assert.False(File.Exists(Path.Combine(root, "current", "absent.txt")));
        }
        finally
        {
            DeleteLabRoot(root);
        }
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("current/../../outside.txt")]
    [InlineData("current/../version.txt")]
    [InlineData("current//version.txt")]
    [InlineData("C:/Windows/secret.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("current:stream")]
    public async Task AssessAsync_RejectsEscapingAndAmbiguousPaths(string path)
    {
        var root = CreateLabRoot();
        try
        {
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => Create().AssessAsync(
                    Package, root,
                    new[] { Expect(path, Encoding.UTF8.GetBytes("expected")) },
                    CancellationToken.None));
        }
        finally
        {
            DeleteLabRoot(root);
        }
    }

    [Fact]
    public async Task AssessAsync_DuplicatePathsFailBeforeAnyHashRead()
    {
        var tool = new ThrowIfUsedHashTool();
        var service = new InstallerPayloadEvidenceService(tool);
        var files = new[]
        {
            Expect("current/test.txt", Encoding.UTF8.GetBytes("same")),
            Expect("current\\test.txt", Encoding.UTF8.GetBytes("same"))
        };
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => service.AssessAsync(Package, "C:/Fixture", files,
                CancellationToken.None));
        Assert.Equal(0, tool.ReadCalls);
    }

    [Fact]
    public async Task AssessAsync_ReadFailureCannotBecomeMatchingEvidence()
    {
        var service = new InstallerPayloadEvidenceService(new ThrowIfUsedHashTool());
        await Assert.ThrowsAsync<IOException>(
            () => service.AssessAsync(
                Package, "C:/Fixture",
                new[] { Expect("current/test.txt", Encoding.UTF8.GetBytes("expected")) },
                CancellationToken.None));
    }

    [Fact]
    public async Task AssessAsync_NoExpectedFilesDoesNotClaimAnEmptyMatch()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => Create().AssessAsync(
                Package, Path.GetTempPath(),
                Array.Empty<InstallerExpectedPayloadFile>(),
                CancellationToken.None));
    }

    [Fact]
    public async Task HashTool_RejectsSymlinkToFileOutsideInstallRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateLabRoot();
        var outside = CreateLabRoot();
        try
        {
            var source = Path.Combine(outside, "external.txt");
            await File.WriteAllTextAsync(source, "Do not read via symbolic link");
            var link = Path.Combine(root, "shortcut.txt");
            File.CreateSymbolicLink(link, source);

            await Assert.ThrowsAsync<IOException>(
                () => new InstalledPayloadHashTool().ComputeSha256Async(
                    root, "shortcut.txt", CancellationToken.None));
        }
        finally
        {
            DeleteLabRoot(root);
            DeleteLabRoot(outside);
        }
    }

    [Fact]
    public async Task HashTool_RejectsSymlinkDirectoryOnPathToFile()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateLabRoot();
        var outside = CreateLabRoot();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(outside, "external.txt"), "external");
            Directory.CreateSymbolicLink(
                Path.Combine(root, "redirect"), outside);

            await Assert.ThrowsAsync<IOException>(
                () => new InstalledPayloadHashTool().ComputeSha256Async(
                    root, "redirect/external.txt", CancellationToken.None));
        }
        finally
        {
            DeleteLabRoot(root);
            DeleteLabRoot(outside);
        }
    }

    private static InstallerPayloadEvidenceService Create()
        => new(new InstalledPayloadHashTool());

    private static InstallerExpectedPayloadFile Expect(string path, byte[] content)
        => new(path, Digest(content));

    private static ArtifactDigest Digest(byte[] bytes)
        => ArtifactDigest.ParseSha256(
            Convert.ToHexString(SHA256.HashData(bytes)));

    private static string CreateLabRoot()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "TurkuazInstaller-PayloadEvidence-LAB",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteLabRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private sealed class ThrowIfUsedHashTool : IInstalledPayloadHashTool
    {
        public int ReadCalls { get; private set; }

        public Task<string?> ComputeSha256Async(
            string installRoot, string relativePath,
            CancellationToken cancellationToken)
        {
            ReadCalls++;
            throw new IOException("Simulated unreadable file.");
        }
    }
}
