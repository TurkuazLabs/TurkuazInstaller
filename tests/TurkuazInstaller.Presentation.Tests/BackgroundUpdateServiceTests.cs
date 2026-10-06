// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/BackgroundUpdateServiceTests.cs
// 📌 Amac: Session background update coordinator enablement, concurrency guard ve entry failure izolasyonunu test eder
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Disabled/busy cycle, entry failure izolasyonu ve localized cycle ozet metnini dogrular
//
// Bagimli Oldugu Katman: Service | View | Config

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.Branding;
using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.Services;
using TurkuazInstaller.Presentation.ViewModels;
using Xunit;

namespace TurkuazInstaller.Presentation.Tests;

public sealed class BackgroundUpdateServiceTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task RunOnceAsync_DisabledPolicy_DoesNotCallRuntime()
    {
        var runtime =
            new StubRuntimeService();

        var viewModel =
            new MainWindowViewModel();

        using var service =
            new BackgroundUpdateService(
                runtime,
                viewModel,
                BackgroundUpdatePolicy.Disabled());

        await service.RunOnceAsync();

        Assert.Equal(
            0,
            runtime.CheckCalls);

        Assert.Equal(
            InstallerUiLabels.BackgroundUpdatesDisabled,
            viewModel.BackgroundUpdateStatus);
    }

    [Fact]
    public async Task RunOnceAsync_BusyInstaller_SkipsCycle()
    {
        var runtime =
            new StubRuntimeService();

        var viewModel =
            new MainWindowViewModel
            {
                IsBusy = true
            };

        using var service =
            new BackgroundUpdateService(
                runtime,
                viewModel,
                CreatePolicy());

        await service.RunOnceAsync();

        Assert.Equal(
            0,
            runtime.CheckCalls);

        Assert.Equal(
            InstallerUiLabels.BackgroundUpdatesWaiting,
            viewModel.BackgroundUpdateStatus);
    }

    [Fact]
    public async Task RunOnceAsync_IsolatesEntryFailureAndCountsAvailableInstalledPackage()
    {
        var runtime =
            new StubRuntimeService();

        runtime.Results["alpha-app"] =
            CreateResult(
                "alpha-app",
                "1.0.0",
                "1.1.0",
                UpdateAvailability.Available);

        runtime.Results["beta-app"] =
            CreateResult(
                "beta-app",
                "2.0.0",
                "2.0.0",
                UpdateAvailability.Current);

        runtime.Failures.Add(
            "broken-app");

        var viewModel =
            new MainWindowViewModel();

        using var service =
            new BackgroundUpdateService(
                runtime,
                viewModel,
                CreatePolicy());

        await service.RunOnceAsync();

        Assert.Equal(
            3,
            runtime.CheckCalls);

        Assert.False(
            viewModel.IsBackgroundUpdateCheckRunning);

        Assert.Equal(
            "Arka plan kontrolu tamamlandi: 2 kontrol, 1 guncelleme, 1 hata",
            viewModel.BackgroundUpdateStatus);
    }

    [Fact]
    public async Task RunOnceAsync_UsesLocalizedCycleSummary()
    {
        var runtime =
            new StubRuntimeService();

        runtime.Results["alpha-app"] =
            CreateResult(
                "alpha-app",
                "1.0.0",
                "1.1.0",
                UpdateAvailability.Available);

        runtime.Results["beta-app"] =
            CreateResult(
                "beta-app",
                "2.0.0",
                "2.0.0",
                UpdateAvailability.Current);

        runtime.Failures.Add(
            "broken-app");

        var catalog =
            new InstallerUiCatalog(
                new InstallerUiProfile(
                    "en-US",
                    InstallerBrandingProfile.Empty,
                    new Dictionary<InstallerUiLabelKey, string>
                    {
                        [InstallerUiLabelKey.BackgroundUpdatesWaiting] =
                            "Waiting",
                        [InstallerUiLabelKey.BackgroundUpdatesChecking] =
                            "Checking",
                        [InstallerUiLabelKey.BackgroundUpdatesCompletedPrefix] =
                            "Background check complete",
                        [InstallerUiLabelKey.BackgroundUpdatesCheckedPrefix] =
                            "checked",
                        [InstallerUiLabelKey.BackgroundUpdatesAvailablePrefix] =
                            "updates",
                        [InstallerUiLabelKey.BackgroundUpdatesFailurePrefix] =
                            "failures"
                    }));

        var viewModel =
            new MainWindowViewModel(
                catalog);

        using var service =
            new BackgroundUpdateService(
                runtime,
                viewModel,
                CreatePolicy(),
                catalog);

        await service.RunOnceAsync();

        Assert.Equal(
            "Background check complete: 2 checked, 1 updates, 1 failures",
            viewModel.BackgroundUpdateStatus);
    }

    private static BackgroundUpdatePolicy CreatePolicy()
    {
        return new BackgroundUpdatePolicy(
            true,
            TimeSpan.FromMinutes(30),
            new[]
            {
                new BackgroundUpdatePolicyEntry(
                    PackageId.Parse(
                        "alpha-app"),
                    ReleaseChannel.Stable,
                    "https://updates.example.test/alpha.yml"),
                new BackgroundUpdatePolicyEntry(
                    PackageId.Parse(
                        "beta-app"),
                    ReleaseChannel.Beta,
                    "https://updates.example.test/beta.yml"),
                new BackgroundUpdatePolicyEntry(
                    PackageId.Parse(
                        "broken-app"),
                    ReleaseChannel.Stable,
                    "https://updates.example.test/broken.yml")
            });
    }

    private static UpdateCheckResult CreateResult(
        string packageId,
        string installedVersion,
        string latestVersion,
        UpdateAvailability availability)
    {
        var parsedPackageId =
            PackageId.Parse(
                packageId);

        var installed =
            new InstalledPackageState(
                parsedPackageId,
                SemanticVersion.Parse(
                    installedVersion),
                ReleaseChannel.Stable,
                string.Concat(
                    "C:/Apps/",
                    packageId));

        var latest =
            new PackageRelease(
                parsedPackageId,
                SemanticVersion.Parse(
                    latestVersion),
                ReleaseChannel.Stable,
                new ArtifactDescriptor(
                    new Uri(
                        string.Concat(
                            "https://updates.example.test/",
                            packageId,
                            ".zip")),
                    ArtifactDigest.ParseSha256(
                        Digest),
                    1024));

        return new UpdateCheckResult(
            availability,
            latest,
            installed);
    }

    private sealed class StubRuntimeService
        : IInstallerRuntimeService
    {
        public Dictionary<string, UpdateCheckResult>
            Results
        {
            get;
        } = new(
            StringComparer.Ordinal);

        public HashSet<string> Failures
        {
            get;
        } = new(
            StringComparer.Ordinal);

        public int CheckCalls
        {
            get;
            private set;
        }

        public Task<UpdateCheckResult> CheckUpdateAsync(
            InstallerUpdateCheckRequest request,
            CancellationToken cancellationToken)
        {
            CheckCalls++;

            if (Failures.Contains(
                    request.PackageId))
            {
                throw new InvalidOperationException(
                    "Simulated background check failure.");
            }

            return Task.FromResult(
                Results[request.PackageId]);
        }

        public Task ExecuteAsync(
            InstallerDesktopRequest request,
            IProgress<InstallerOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Mutation must not run during background update tests.");
        }

        public Task ResumeAsync(
            PackageId packageId,
            IProgress<InstallerOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "Resume must not run during background update tests.");
        }
    }
}
