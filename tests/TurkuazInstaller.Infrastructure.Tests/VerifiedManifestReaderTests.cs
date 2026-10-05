// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/VerifiedManifestReaderTests.cs
// 📌 Amac: Manifest YAML parse isleminden once detached signature verification yapildigini contract testiyle dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Invalid signature durumunda malformed YAML'in parsera ulasmadigini ve valid signature sonrasinda normal parse akisinin calistigini kanitlar
//
// Bagimli Oldugu Katman: Tool | Service

using System.Text;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.Verification;
using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class VerifiedManifestReaderTests
{
    [Fact]
    public async Task ReadAsync_RejectsSignatureBeforeYamlParse()
    {
        var reader =
            new VerifiedManifestReader(
                new InstallerManifestReader(),
                new FakeManifestSignatureVerifier(
                    VerificationResult.Failed(
                        VerificationFailure.SignatureInvalid,
                        "Signature rejected.")));

        var malformedYaml =
            Encoding.UTF8.GetBytes(
                "this: [is: not: valid");

        var exception =
            await Assert.ThrowsAsync<InvalidDataException>(
                () =>
                    reader.ReadAsync(
                        malformedYaml,
                        Encoding.UTF8.GetBytes(
                            ProviderTestData.DetachedSignature),
                        PackageId.Parse(
                            ProviderTestData.PackageId),
                        CancellationToken.None));

        Assert.Equal(
            "Signature rejected.",
            exception.Message);
    }

    [Fact]
    public async Task ReadAsync_ParsesOnlyAfterSignaturePasses()
    {
        var signatureVerifier =
            new FakeManifestSignatureVerifier();

        var reader =
            new VerifiedManifestReader(
                new InstallerManifestReader(),
                signatureVerifier);

        var release =
            await reader.ReadAsync(
                Encoding.UTF8.GetBytes(
                    ProviderTestData.Manifest(
                        ReleaseChannel.Stable,
                        "6.0.0")),
                Encoding.UTF8.GetBytes(
                    ProviderTestData.DetachedSignature),
                PackageId.Parse(
                    ProviderTestData.PackageId),
                CancellationToken.None);

        Assert.Equal(
            "6.0.0",
            release.Version.ToString());

        Assert.Equal(
            1,
            signatureVerifier.CallCount);
    }
}
