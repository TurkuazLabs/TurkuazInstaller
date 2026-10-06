// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/UpdateCheckServiceTests.cs
// 📌 Amac: UpdateCheckService surum kararlarini fake port implementasyonlariyla dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Availability kararina ek olarak exact skip ve maximum-version pin policy sonucunu dogrular
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Contracts.Updates;
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

        Assert.Equal(
            UpdateAvailability.Available,
            result.Availability);

        Assert.Same(
            state,
            result.InstalledState);

        Assert.Equal(
            "1.1.0",
            result.LatestRelease?.Version.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLatestMatchesInstalled_ReturnsCurrent()
    {
        var state = new InstalledPackageState(Package, SemanticVersion.Parse("1.1.0"), ReleaseChannel.Stable, "C:/Apps/Example");
        var service = new UpdateCheckService(new StubReleaseProvider(CreateRelease("1.1.0")), new StubStateRepository(state));

        var result = await service.ExecuteAsync(Package, ReleaseChannel.Stable, CancellationToken.None);

        Assert.Equal(
            UpdateAvailability.Current,
            result.Availability);

        Assert.Same(
            state,
            result.InstalledState);

        Assert.Equal(
            "1.1.0",
            result.LatestRelease?.Version.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_WhenLatestVersionIsSkipped_ReturnsSkipped()
    {
        var state =
            new InstalledPackageState(
                Package,
                SemanticVersion.Parse("1.0.0"),
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var policy =
            new VersionUpdatePolicy(
                Package,
                ReleaseChannel.Stable,
                null,
                new[]
                {
                    SemanticVersion.Parse("1.1.0")
                });

        var service =
            new UpdateCheckService(
                new StubReleaseProvider(
                    CreateRelease("1.1.0")),
                new StubStateRepository(
                    state),
                new StubVersionPolicyRepository(
                    policy));

        var result =
            await service.ExecuteAsync(
                Package,
                ReleaseChannel.Stable,
                CancellationToken.None);

        Assert.Equal(
            UpdateAvailability.Skipped,
            result.Availability);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLatestExceedsMaximumVersion_ReturnsPinned()
    {
        var state =
            new InstalledPackageState(
                Package,
                SemanticVersion.Parse("1.0.0"),
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var policy =
            new VersionUpdatePolicy(
                Package,
                ReleaseChannel.Stable,
                SemanticVersion.Parse("1.0.5"),
                Array.Empty<SemanticVersion>());

        var service =
            new UpdateCheckService(
                new StubReleaseProvider(
                    CreateRelease("1.1.0")),
                new StubStateRepository(
                    state),
                new StubVersionPolicyRepository(
                    policy));

        var result =
            await service.ExecuteAsync(
                Package,
                ReleaseChannel.Stable,
                CancellationToken.None);

        Assert.Equal(
            UpdateAvailability.Pinned,
            result.Availability);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReleaseDoesNotExist_ReturnsReleaseNotFound()
    {
        var service = new UpdateCheckService(new StubReleaseProvider(null), new StubStateRepository(null));

        var result = await service.ExecuteAsync(Package, ReleaseChannel.Stable, CancellationToken.None);

        Assert.Equal(
            UpdateAvailability.ReleaseNotFound,
            result.Availability);

        Assert.Null(
            result.LatestRelease);

        Assert.Null(
            result.InstalledState);
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

    private sealed class StubVersionPolicyRepository
        : IVersionUpdatePolicyRepository
    {
        private readonly VersionUpdatePolicy? _policy;

        public StubVersionPolicyRepository(
            VersionUpdatePolicy? policy)
        {
            _policy = policy;
        }

        public Task<VersionUpdatePolicy?> GetAsync(
            PackageId packageId,
            ReleaseChannel channel,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _policy);
        }
    }

    private sealed class StubStateRepository : IInstallStateRepository
    {
        private readonly InstalledPackageState? _state;
        public StubStateRepository(InstalledPackageState? state) => _state = state;

        public Task<InstalledPackageState?> GetAsync(PackageId packageId, CancellationToken cancellationToken)
            => Task.FromResult(_state);

        public Task<IReadOnlyList<InstalledPackageState>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<InstalledPackageState> states =
                _state is null
                    ? Array.Empty<InstalledPackageState>()
                    : new[]
                    {
                        _state
                    };

            return Task.FromResult(states);
        }

        public Task SaveAsync(InstalledPackageState state, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DeleteAsync(PackageId packageId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
