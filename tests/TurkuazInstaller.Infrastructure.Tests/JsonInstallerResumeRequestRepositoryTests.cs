// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/JsonInstallerResumeRequestRepositoryTests.cs
// 📌 Amac: Reboot resume request JSON repository roundtrip ve delete davranisini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Operation, release identity, manifest sources ve package-scoped persistence alanlarini kapsar
//
// Bagimli Oldugu Katman: Repo | Service

using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Operations;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class JsonInstallerResumeRequestRepositoryTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task SaveGetDelete_RoundTripsResumeRequest()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var repository =
                new JsonInstallerResumeRequestRepository(
                    new JsonInstallerResumeRequestRepositoryOptions(
                        root));

            var request =
                new InstallerResumeRequest(
                    PackageId.Parse(
                        "example-app"),
                    InstallerOperationType.Update,
                    SemanticVersion.Parse(
                        "2.4.0"),
                    ArtifactDigest.ParseSha256(
                        Digest),
                    ReleaseChannel.Stable,
                    "https://example.invalid/installer-manifest.yml",
                    null,
                    DateTimeOffset.Parse(
                        "2026-10-05T18:00:00+00:00"));

            await repository.SaveAsync(
                request,
                CancellationToken.None);

            var loaded =
                await repository.GetAsync(
                    request.PackageId,
                    CancellationToken.None);

            Assert.NotNull(
                loaded);

            Assert.Equal(
                request,
                loaded);

            await repository.DeleteAsync(
                request.PackageId,
                CancellationToken.None);

            Assert.Null(
                await repository.GetAsync(
                    request.PackageId,
                    CancellationToken.None));
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
