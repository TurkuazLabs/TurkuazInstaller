// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/YamlManifestTrustPolicyRepositoryTests.cs
// 📌 Amac: External YAML manifest trust store repositorysinin package bazli pin okuma ve fail-closed validation davranisini dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Valid policy, missing file ve duplicate package id senaryolarini gecici dosyalarla test eder
//
// Bagimli Oldugu Katman: Repo | Service

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Repositories;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class YamlManifestTrustPolicyRepositoryTests
{
    [Fact]
    public async Task GetAsync_LoadsPinnedPublisherPolicy()
    {
        var path =
            CreateTemporaryPath();

        try
        {
            await File.WriteAllTextAsync(
                path,
                TrustStoreYaml());

            var repository =
                CreateRepository(
                    path);

            var policy =
                await repository.GetAsync(
                    PackageId.Parse(
                        ProviderTestData.PackageId),
                    CancellationToken.None);

            Assert.NotNull(
                policy);

            Assert.Equal(
                "CN=Example Software",
                policy.PublisherSubject);

            Assert.Equal(
                ProviderTestData.CertificateSha256,
                policy.CertificateSha256);
        }
        finally
        {
            DeleteIfExists(
                path);
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsNullWhenTrustStoreDoesNotExist()
    {
        var path =
            CreateTemporaryPath();

        var repository =
            CreateRepository(
                path);

        var policy =
            await repository.GetAsync(
                PackageId.Parse(
                    ProviderTestData.PackageId),
                CancellationToken.None);

        Assert.Null(
            policy);
    }

    [Fact]
    public async Task GetAsync_RejectsDuplicatePackageId()
    {
        var path =
            CreateTemporaryPath();

        try
        {
            await File.WriteAllTextAsync(
                path,
                string.Concat(
                    TrustStoreYaml(),
                    Environment.NewLine,
                    "  - id: example-app",
                    Environment.NewLine,
                    "    publisher_subject: \"CN=Example Software\"",
                    Environment.NewLine,
                    "    certificate_sha256: ",
                    ProviderTestData.CertificateSha256,
                    Environment.NewLine));

            var repository =
                CreateRepository(
                    path);

            await Assert.ThrowsAsync<FormatException>(
                () =>
                    repository.GetAsync(
                        PackageId.Parse(
                            ProviderTestData.PackageId),
                        CancellationToken.None));
        }
        finally
        {
            DeleteIfExists(
                path);
        }
    }

    private static YamlManifestTrustPolicyRepository CreateRepository(
        string path)
    {
        return new YamlManifestTrustPolicyRepository(
            new YamlManifestTrustPolicyRepositoryOptions(
                path));
    }

    private static string TrustStoreYaml()
    {
        return $$"""
schema_version: 1
packages:
  - id: {{ProviderTestData.PackageId}}
    publisher_subject: "CN=Example Software"
    certificate_sha256: {{ProviderTestData.CertificateSha256}}
""";
    }

    private static string CreateTemporaryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            string.Concat(
                "turkuaz-manifest-trust-",
                Guid.NewGuid()
                    .ToString("N"),
                ".yml"));
    }

    private static void DeleteIfExists(
        string path)
    {
        if (File.Exists(path))
        {
            File.Delete(
                path);
        }
    }
}
