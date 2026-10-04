// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/UpdateCheckServiceTests.cs
// 📌 Amac: UpdateCheckService surum kararlarini fake port implementasyonlariyla dogrular
// 📌 Modul - Test CSharp
// Version: 0.3.0
// Aciklama: Available, current ve release-not-found senaryolarini kapsar
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using Xunit;

namespace TurkuazInstaller.Application.Tests;

public sealed class UpdateCheckServiceTests
{
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly PackageId Package = PackageId.Parse("example-app");

    [Fact]
    public async Task ExecuteAsync_WhenLatestIsNewer_ReturnsAvailable()
    {
        var state = new InstalledPackageState(Package, SemanticVersion.Parse("1.0.0"), ReleaseChannel.Stable, "C:/Apps/Example");
        var service = new UpdateCheckService(new StubReleaseProvider(CreateRelease("1.1.0")), new StubStateRepository(state));

        var result = await service.ExecuteAsync(Package, ReleaseChannel.Stable, CancellationToken.None);

        Assert.Equal(UpdateAvailability.Available, result.Availability);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLatestMatchesInstalled_ReturnsCurrent()
    {
        var state = new InstalledPackageState(Package, SemanticVersion.Parse("1.1.0"), ReleaseChannel.Stable, "C:/Apps/Example");
        var service = new UpdateCheckService(new StubReleaseProvider(CreateRelease("1.1.0")), new StubStateRepository(state));

        var result = await service.ExecuteAsync(Package, ReleaseChannel.Stable, CancellationToken.None);

        Assert.Equal(UpdateAvailability.Current, result.Availability);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReleaseDoesNotExist_ReturnsReleaseNotFound()
    {
        var service = new UpdateCheckService(new StubReleaseProvider(null), new StubStateRepository(null));

        var result = await service.ExecuteAsync(Package, ReleaseChannel.Stable, CancellationToken.None);

        Assert.Equal(UpdateAvailability.ReleaseNotFound, result.Availability);
    }

    private static PackageRelease CreateRelease(string version)
    {
        return new PackageRelease(
            Package,
            SemanticVersion.Parse(version),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri("https://example.invalid/example-app.zip"),
                ArtifactDigest.ParseSha256(Digest),
                1024));
    }

    private sealed class StubReleaseProvider : IReleaseProvider
    {
        private readonly PackageRelease? _release;
        public StubReleaseProvider(PackageRelease? release) => _release = release;

        public Task<PackageRelease?> GetLatestReleaseAsync(PackageId packageId, ReleaseChannel channel, CancellationToken cancellationToken)
            => Task.FromResult(_release);
    }

    private sealed class StubStateRepository : IInstallStateRepository
    {
        private readonly InstalledPackageState? _state;
        public StubStateRepository(InstalledPackageState? state) => _state = state;

        public Task<InstalledPackageState?> GetAsync(PackageId packageId, CancellationToken cancellationToken)
            => Task.FromResult(_state);

        public Task SaveAsync(InstalledPackageState state, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
