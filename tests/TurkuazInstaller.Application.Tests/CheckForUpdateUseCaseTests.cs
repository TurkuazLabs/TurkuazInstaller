// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/CheckForUpdateUseCaseTests.cs
// 📌 Amac: CheckForUpdate use-case surum kararlarini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 0.1.0
// Aciklama: Provider bagimsiz update available ve current senaryolarini kapsar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using Xunit;

namespace TurkuazInstaller.Application.Tests;

public sealed class CheckForUpdateUseCaseTests
{
    private static readonly ProductId Product = ProductId.Parse("jhoster");
    private static readonly Version LatestVersion = new(4, 6, 0);
    private static readonly Uri PackageUri = new("https://example.invalid/jhoster.pkg");

    [Fact]
    public async Task ExecuteAsync_WhenLatestIsNewer_ReturnsAvailable()
    {
        var releaseSource = new StubReleaseSource(CreateRelease());
        var useCase = new CheckForUpdateUseCase(releaseSource);

        var result = await useCase.ExecuteAsync(
            Product,
            new Version(4, 5, 0),
            ReleaseChannel.Stable,
            CpuArchitecture.X64,
            CancellationToken.None);

        Assert.Equal(UpdateAvailability.Available, result.Availability);
        Assert.Equal(LatestVersion, result.LatestRelease?.Version);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInstalledMatchesLatest_ReturnsCurrent()
    {
        var releaseSource = new StubReleaseSource(CreateRelease());
        var useCase = new CheckForUpdateUseCase(releaseSource);

        var result = await useCase.ExecuteAsync(
            Product,
            LatestVersion,
            ReleaseChannel.Stable,
            CpuArchitecture.X64,
            CancellationToken.None);

        Assert.Equal(UpdateAvailability.Current, result.Availability);
    }

    private static ReleaseDescriptor CreateRelease()
    {
        var package = new PackageDescriptor(
            PackageUri,
            CpuArchitecture.X64,
            1024,
            "0123456789abcdef");

        return new ReleaseDescriptor(
            Product,
            LatestVersion,
            ReleaseChannel.Stable,
            package,
            false,
            string.Empty);
    }

    private sealed class StubReleaseSource : IReleaseSource
    {
        private readonly ReleaseDescriptor _release;

        public StubReleaseSource(ReleaseDescriptor release)
        {
            _release = release;
        }

        public Task<ReleaseDescriptor?> GetLatestAsync(
            ProductId productId,
            ReleaseChannel channel,
            CpuArchitecture architecture,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ReleaseDescriptor?>(_release);
        }
    }
}
