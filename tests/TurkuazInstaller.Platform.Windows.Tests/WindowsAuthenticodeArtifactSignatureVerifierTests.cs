// Dosya: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsAuthenticodeArtifactSignatureVerifierTests.cs
// Amac: Windows'un gercek guven zinciriyle imzalanmis OS binary'sine karsi Authenticode, publisher ve pin kontrollerini calistirir
// Version: 0.3.0
// Guvenlik: Trust store / antivirus ayari degismez, mevcut Microsoft-imzali sistem binary'si read-only kullanilir.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsAuthenticodeArtifactSignatureVerifierTests
{
    private static readonly Lazy<SignedFixture> TrustedFixture =
        new(FindTrustedSignedFixture);

    [Fact]
    public async Task VerifyAsync_TrustedSystemBinaryWithPinnedSigner_Succeeds()
    {
        var fixture = TrustedFixture.Value;

        var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
            .VerifyAsync(fixture.FilePath, fixture.ExpectedSignature, CancellationToken.None);

        Assert.True(result.IsValid, result.Message);
    }

    [Fact]
    public async Task VerifyAsync_TrustedBinaryWithIncorrectPublisher_FailsClosed()
    {
        var fixture = TrustedFixture.Value;
        var wrongPublisher = new ArtifactSignatureDescriptor(
            ArtifactSignatureAlgorithm.Authenticode,
            "CN=Not The Real Publisher",
            fixture.ExpectedSignature.CertificateSha256);

        var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
            .VerifyAsync(fixture.FilePath, wrongPublisher, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_TrustedBinaryWithIncorrectCertificatePin_FailsClosed()
    {
        var fixture = TrustedFixture.Value;
        var incorrectPin = new ArtifactSignatureDescriptor(
            ArtifactSignatureAlgorithm.Authenticode,
            fixture.ExpectedSignature.PublisherSubject,
            new string('0', 64));

        var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
            .VerifyAsync(fixture.FilePath, incorrectPin, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_ModifiedCopyOfTrustedBinary_FailsClosed()
    {
        var fixture = TrustedFixture.Value;
        var root = Path.Combine(
            Path.GetTempPath(),
            "TurkuazInstallerAuthenticodeTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        var changedPath = Path.Combine(root, Path.GetFileName(fixture.FilePath));

        try
        {
            File.Copy(fixture.FilePath, changedPath);

            await using (var file = new FileStream(
                changedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.True(file.Length > 8192, "Signed OS fixture was unexpectedly small.");
                file.Position = 4096; // PE file content, not the certificate table.
                var originalByte = file.ReadByte();
                Assert.True(originalByte >= 0);
                file.Position = 4096;
                file.WriteByte((byte)(originalByte ^ 0x01));
                await file.FlushAsync();
            }

            var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
                .VerifyAsync(changedPath, fixture.ExpectedSignature, CancellationToken.None);

            Assert.False(result.IsValid);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_NonexistentArtifact_FailsClosed()
    {
        var fixture = TrustedFixture.Value;
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            "TurkuazInstallerMissingAuthenticode",
            Guid.NewGuid().ToString("N"),
            "missing.exe");

        var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
            .VerifyAsync(missingPath, fixture.ExpectedSignature, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_UnsignedLocalExecutable_FailsClosedWithoutTrustChanges()
    {
        var fixture = TrustedFixture.Value;
        var root = Path.Combine(
            Path.GetTempPath(),
            "TurkuazInstallerUnsignedAuthenticodeTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        var unsignedPath = Path.Combine(root, "unsigned.exe");

        try
        {
            // Deliberately unsigned, non-executable test data: never launch it.
            await File.WriteAllTextAsync(unsignedPath, "LAB unsigned file");

            var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
                .VerifyAsync(unsignedPath, fixture.ExpectedSignature, CancellationToken.None);

            Assert.False(result.IsValid);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_TrustedBinaryWithAlternateSignatureAlgorithm_FailsClosed()
    {
        var fixture = TrustedFixture.Value;
        var unsupportedSignature = new ArtifactSignatureDescriptor(
            (ArtifactSignatureAlgorithm)int.MaxValue,
            fixture.ExpectedSignature.PublisherSubject,
            fixture.ExpectedSignature.CertificateSha256);

        var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
            .VerifyAsync(fixture.FilePath, unsupportedSignature, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task VerifyAsync_UnsignedFixtureWithMatchingExpectedIdentity_FailsClosed()
    {
        var fixture = TrustedFixture.Value;
        var root = Path.Combine(
            Path.GetTempPath(),
            "TurkuazInstallerUnsignedIdentityTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var unsignedPath = Path.Combine(root, "unsigned.exe");
            await File.WriteAllTextAsync(unsignedPath, "unsigned fixture");

            // Publisher names and certificate pins supplied by callers are not trust evidence.
            var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
                .VerifyAsync(unsignedPath, fixture.ExpectedSignature, CancellationToken.None);
            Assert.False(result.IsValid);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_PreCancelledRequest_DoesNotBeginTrustVerification()
    {
        var fixture = TrustedFixture.Value;
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new WindowsAuthenticodeArtifactSignatureVerifier()
                .VerifyAsync(fixture.FilePath, fixture.ExpectedSignature, source.Token));
    }

    private static SignedFixture FindTrustedSignedFixture()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidOperationException("Real Authenticode smoke requires Windows.");
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidates = new[]
        {
            Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe"),
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            Path.Combine(Environment.SystemDirectory, "notepad.exe")
        };

        var verifier = new WindowsAuthenticodeArtifactSignatureVerifier();
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
#pragma warning disable SYSLIB0057
                using var certificate = X509Certificate.CreateFromSignedFile(candidate);
#pragma warning restore SYSLIB0057
                var signature = new ArtifactSignatureDescriptor(
                    ArtifactSignatureAlgorithm.Authenticode,
                    certificate.Subject,
                    certificate.GetCertHashString(HashAlgorithmName.SHA256));

                // A catalogue-only signature is insufficient for this test,
                // which intentionally verifies the embedded signer identity.
                var result = verifier.VerifyAsync(
                    candidate, signature, CancellationToken.None)
                    .GetAwaiter().GetResult();

                if (result.IsValid)
                {
                    return new SignedFixture(candidate, signature);
                }
            }
            catch (CryptographicException)
            {
                // Try the next known OS binary with an embedded signature.
            }
        }

        throw new InvalidOperationException(
            "No Windows-trusted Authenticode binary with an embedded signer " +
            "was found among the known Windows/PowerShell fixture paths.");
    }

    private sealed record SignedFixture(
        string FilePath,
        ArtifactSignatureDescriptor ExpectedSignature);
}
