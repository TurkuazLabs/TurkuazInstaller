// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/FileReleaseProviderTests.cs
// 📌 Amac: Local file providerin beta manifestini detached signature ile birlikte okuyabildigini dogrular
// 📌 Modul - Test CSharp
// Version: 1.2.0
// Aciklama: Air-gapped manifest + .p7s basari akisina ek olarak local manifest/signature byte limitlerini ve verifier-oncesi reddi kapsar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.File;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class FileReleaseProviderTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_RejectsOversizedLocalManifestBeforeVerification()
    {
        var stablePath =
            Path.GetTempFileName();

        var betaPath =
            Path.GetTempFileName();

        var betaSignaturePath =
            ManifestSignatureConventions
                .GetDetachedSignaturePath(
                    betaPath);

        try
        {
            await System.IO.File.WriteAllBytesAsync(
                betaPath,
                new byte[
                    ManifestContentLimits.MaximumManifestBytes +
                    1]);

            await System.IO.File.WriteAllTextAsync(
                betaSignaturePath,
                ProviderTestData.DetachedSignature);

            var signatureVerifier =
                new FakeManifestSignatureVerifier();

            var provider =
                new FileReleaseProvider(
                    new InstallerManifestReader(),
                    signatureVerifier,
                    new FileReleaseProviderOptions(
                        stablePath,
                        betaPath));

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    provider.GetLatestReleaseAsync(
                        PackageId.Parse(
                            ProviderTestData.PackageId),
                        ReleaseChannel.Beta,
                        CancellationToken.None));

            Assert.Equal(
                0,
                signatureVerifier.CallCount);
        }
        finally
        {
            System.IO.File.Delete(
                stablePath);
            System.IO.File.Delete(
                betaPath);
            System.IO.File.Delete(
                betaSignaturePath);
        }
    }

    [Fact]
    public async Task GetLatestReleaseAsync_RejectsOversizedLocalSignatureBeforeVerification()
    {
        var stablePath =
            Path.GetTempFileName();

        var betaPath =
            Path.GetTempFileName();

        var betaSignaturePath =
            ManifestSignatureConventions
                .GetDetachedSignaturePath(
                    betaPath);

        try
        {
            await System.IO.File.WriteAllTextAsync(
                betaPath,
                ProviderTestData.Manifest(
                    ReleaseChannel.Beta,
                    "3.0.0-beta.1"));

            await System.IO.File.WriteAllBytesAsync(
                betaSignaturePath,
                new byte[
                    ManifestContentLimits.MaximumDetachedSignatureBytes +
                    1]);

            var signatureVerifier =
                new FakeManifestSignatureVerifier();

            var provider =
                new FileReleaseProvider(
                    new InstallerManifestReader(),
                    signatureVerifier,
                    new FileReleaseProviderOptions(
                        stablePath,
                        betaPath));

            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    provider.GetLatestReleaseAsync(
                        PackageId.Parse(
                            ProviderTestData.PackageId),
                        ReleaseChannel.Beta,
                        CancellationToken.None));

            Assert.Equal(
                0,
                signatureVerifier.CallCount);
        }
        finally
        {
            System.IO.File.Delete(
                stablePath);
            System.IO.File.Delete(
                betaPath);
            System.IO.File.Delete(
                betaSignaturePath);
        }
    }

    [Fact]
    public async Task GetLatestReleaseAsync_LoadsVerifiedBetaManifest()
    {
        var stablePath =
            Path.GetTempFileName();

        var betaPath =
            Path.GetTempFileName();

        var betaSignaturePath =
            ManifestSignatureConventions
                .GetDetachedSignaturePath(
                    betaPath);

        try
        {
            await System.IO.File.WriteAllTextAsync(
                betaPath,
                ProviderTestData.Manifest(
                    ReleaseChannel.Beta,
                    "3.0.0-beta.1"));

            await System.IO.File.WriteAllTextAsync(
                betaSignaturePath,
                ProviderTestData.DetachedSignature);

            var signatureVerifier =
                new FakeManifestSignatureVerifier();

            var provider =
                new FileReleaseProvider(
                    new InstallerManifestReader(),
                    signatureVerifier,
                    new FileReleaseProviderOptions(
                        stablePath,
                        betaPath));

            var release =
                await provider.GetLatestReleaseAsync(
                    PackageId.Parse(
                        ProviderTestData.PackageId),
                    ReleaseChannel.Beta,
                    CancellationToken.None);

            Assert.Equal(
                "3.0.0-beta.1",
                release?.Version.ToString());

            Assert.Equal(
                1,
                signatureVerifier.CallCount);
        }
        finally
        {
            System.IO.File.Delete(
                stablePath);

            System.IO.File.Delete(
                betaPath);

            System.IO.File.Delete(
                betaSignaturePath);
        }
    }
}
