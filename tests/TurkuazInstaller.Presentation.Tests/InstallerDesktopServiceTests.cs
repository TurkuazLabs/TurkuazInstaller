// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/InstallerDesktopServiceTests.cs
// 📌 Amac: InstallerDesktopService progress, success ve recovery state davranisini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.4.0
// Aciklama: Progress/reboot resume/catalog davranisina ek olarak read-only update discovery state ve hata ayrimini test eder
//
// Bagimli Oldugu Katman: Service | View

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Updates;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.Services;
using TurkuazInstaller.Presentation.ViewModels;
using Xunit;

namespace TurkuazInstaller.Presentation.Tests;

public sealed class InstallerDesktopServiceTests
{
    [Fact]
    public async Task RunAsync_Success_UpdatesCompletedState()
    {
        var viewModel = CreateViewModel();
        var runtime = new StubRuntimeService();

        using var service = new InstallerDesktopService(
            viewModel,
            runtime,
            new InstallerResumeLaunchParser());

        await service.RunAsync(
            InstallerOperationKind.Install);

        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.HasError);
        Assert.Equal(100, viewModel.ProgressValue);
        Assert.Equal(
            InstallerUiLabels.Completed,
            viewModel.StatusMessage);
    }

    [Fact]
    public async Task RunAsync_RuntimeFailure_EnablesRetry()
    {
        var viewModel = CreateViewModel();
        var runtime = new StubRuntimeService
        {
            Exception = new InvalidOperationException(
                "Test failure")
        };

        using var service = new InstallerDesktopService(
            viewModel,
            runtime,
            new InstallerResumeLaunchParser());

        await service.RunAsync(
            InstallerOperationKind.Update);

        Assert.True(viewModel.HasError);
        Assert.True(viewModel.CanRetry);
        Assert.Contains(
            "Test failure",
            viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_ReportsProgressWithoutOverwritingCompletedState()
    {
        var viewModel = CreateViewModel();
        var runtime = new StubRuntimeService
        {
            Progress = new InstallerOperationProgress(
                InstallerProgressStage.Verifying,
                35)
        };

        using var service = new InstallerDesktopService(
            viewModel,
            runtime,
            new InstallerResumeLaunchParser());

        await service.RunAsync(
            InstallerOperationKind.Repair);

        Assert.Equal(100, viewModel.ProgressValue);
        Assert.Equal(
            InstallerUiLabels.Completed,
            viewModel.StatusMessage);
    }

    [Fact]
    public async Task StartAsync_RebootResumeArgument_InvokesRuntimeResume()
    {
        var viewModel =
            CreateViewModel();

        var runtime =
            new StubRuntimeService();

        using var service =
            new InstallerDesktopService(
                viewModel,
                runtime,
                new InstallerResumeLaunchParser());

        await service.StartAsync(
            new[]
            {
                "--resume-package",
                "example-app"
            });

        Assert.NotNull(
            runtime.ResumedPackageId);

        Assert.Equal(
            "example-app",
            runtime.ResumedPackageId!.Value);

        Assert.Equal(
            InstallerUiLabels.Completed,
            viewModel.StatusMessage);
    }


    [Fact]
    public async Task StartAsync_NoResume_LoadsInstalledCatalog()
    {
        var viewModel =
            CreateViewModel();

        var catalog =
            new InstalledAppCatalogService(
                new StubStateRepository(
                    new[]
                    {
                        new InstalledPackageState(
                            PackageId.Parse(
                                "installed-app"),
                            SemanticVersion.Parse(
                                "3.2.1"),
                            ReleaseChannel.Stable,
                            "C:/Apps/Installed")
                    }));

        using var service =
            new InstallerDesktopService(
                viewModel,
                new StubRuntimeService(),
                new InstallerResumeLaunchParser(),
                catalog);

        await service.StartAsync(
            Array.Empty<string>());

        var item =
            Assert.Single(
                viewModel.InstalledApps);

        Assert.Equal(
            "installed-app",
            item.PackageId);

        Assert.False(
            viewModel.HasCatalogError);

        Assert.False(
            viewModel.HasError);
    }

    [Fact]
    public async Task StartAsync_CatalogFailure_DoesNotMarkInstallerOperationFailed()
    {
        var viewModel =
            CreateViewModel();

        var catalog =
            new InstalledAppCatalogService(
                new StubStateRepository(
                    Array.Empty<InstalledPackageState>(),
                    new InvalidDataException(
                        "Broken state.")));

        using var service =
            new InstallerDesktopService(
                viewModel,
                new StubRuntimeService(),
                new InstallerResumeLaunchParser(),
                catalog);

        await service.StartAsync(
            Array.Empty<string>());

        Assert.True(
            viewModel.HasCatalogError);

        Assert.Contains(
            "Broken state.",
            viewModel.CatalogErrorMessage);

        Assert.False(
            viewModel.HasError);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_Available_ShowsInstalledAndLatestVersions()
    {
        var viewModel =
            CreateViewModel();

        var installedState =
            new InstalledPackageState(
                PackageId.Parse(
                    "example-app"),
                SemanticVersion.Parse(
                    "1.0.0"),
                ReleaseChannel.Stable,
                "C:/Apps/Example");

        var runtime =
            new StubRuntimeService
            {
                UpdateCheckResult =
                    new UpdateCheckResult(
                        UpdateAvailability.Available,
                        CreateRelease(
                            "1.1.0"),
                        installedState)
            };

        using var service =
            new InstallerDesktopService(
                viewModel,
                runtime,
                new InstallerResumeLaunchParser());

        await service.CheckForUpdatesAsync();

        Assert.Equal(
            "1.0.0",
            viewModel.InstalledVersionText);

        Assert.Equal(
            "1.1.0",
            viewModel.LatestVersionText);

        Assert.Equal(
            InstallerUiLabels.UpdateAvailable,
            viewModel.UpdateDiscoveryStatus);

        Assert.True(
            viewModel.HasUpdateAvailable);

        Assert.False(
            viewModel.HasUpdateDiscoveryError);

        Assert.NotNull(
            runtime.LastUpdateCheckRequest);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_NotInstalled_ShowsLatestWithoutEnablingUpdateAvailable()
    {
        var viewModel =
            CreateViewModel();

        var runtime =
            new StubRuntimeService
            {
                UpdateCheckResult =
                    new UpdateCheckResult(
                        UpdateAvailability.Available,
                        CreateRelease(
                            "2.0.0"),
                        null)
            };

        using var service =
            new InstallerDesktopService(
                viewModel,
                runtime,
                new InstallerResumeLaunchParser());

        await service.CheckForUpdatesAsync();

        Assert.Equal(
            InstallerUiLabels.VersionUnavailable,
            viewModel.InstalledVersionText);

        Assert.Equal(
            "2.0.0",
            viewModel.LatestVersionText);

        Assert.Equal(
            InstallerUiLabels.UpdateNotInstalled,
            viewModel.UpdateDiscoveryStatus);

        Assert.False(
            viewModel.HasUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_RuntimeFailure_DoesNotMarkInstallerOperationFailed()
    {
        var viewModel =
            CreateViewModel();

        var runtime =
            new StubRuntimeService
            {
                UpdateCheckException =
                    new InvalidOperationException(
                        "Discovery failed.")
            };

        using var service =
            new InstallerDesktopService(
                viewModel,
                runtime,
                new InstallerResumeLaunchParser());

        await service.CheckForUpdatesAsync();

        Assert.True(
            viewModel.HasUpdateDiscoveryError);

        Assert.Contains(
            "Discovery failed.",
            viewModel.UpdateDiscoveryErrorMessage);

        Assert.False(
            viewModel.HasError);

        Assert.False(
            viewModel.IsCheckingUpdate);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ResultResetsWhenManifestSourceChanges()
    {
        var viewModel =
            CreateViewModel();

        var runtime =
            new StubRuntimeService
            {
                UpdateCheckResult =
                    new UpdateCheckResult(
                        UpdateAvailability.Available,
                        CreateRelease(
                            "1.1.0"),
                        new InstalledPackageState(
                            PackageId.Parse(
                                "example-app"),
                            SemanticVersion.Parse(
                                "1.0.0"),
                            ReleaseChannel.Stable,
                            "C:/Apps/Example"))
            };

        using var service =
            new InstallerDesktopService(
                viewModel,
                runtime,
                new InstallerResumeLaunchParser());

        await service.CheckForUpdatesAsync();

        Assert.True(
            viewModel.HasUpdateAvailable);

        viewModel.ManifestSource =
            "https://example.invalid/other-manifest.yml";

        Assert.Equal(
            InstallerUiLabels.VersionUnavailable,
            viewModel.InstalledVersionText);

        Assert.Equal(
            InstallerUiLabels.VersionUnavailable,
            viewModel.LatestVersionText);

        Assert.Equal(
            InstallerUiLabels.UpdateNotChecked,
            viewModel.UpdateDiscoveryStatus);

        Assert.False(
            viewModel.HasUpdateAvailable);
    }

    [Fact]
    public async Task RunAsync_WhileUpdateDiscoveryBusy_DoesNotStartMutation()
    {
        var viewModel =
            CreateViewModel();

        viewModel.IsCheckingUpdate = true;

        var runtime =
            new StubRuntimeService();

        using var service =
            new InstallerDesktopService(
                viewModel,
                runtime,
                new InstallerResumeLaunchParser());

        await service.RunAsync(
            InstallerOperationKind.Update);

        Assert.Null(
            runtime.LastRequest);
    }

    [Fact]
    public async Task RunAsync_Uninstall_DoesNotRequireManifestOrTarget()
    {
        var viewModel =
            new MainWindowViewModel
            {
                PackageIdText = "example-app"
            };

        var runtime =
            new StubRuntimeService();

        using var service =
            new InstallerDesktopService(
                viewModel,
                runtime,
                new InstallerResumeLaunchParser());

        await service.RunAsync(
            InstallerOperationKind.Uninstall);

        Assert.False(
            viewModel.HasError);

        Assert.NotNull(
            runtime.LastRequest);

        Assert.Equal(
            InstallerOperationKind.Uninstall,
            runtime.LastRequest.Operation);
    }

    private static PackageRelease CreateRelease(
        string version)
    {
        return new PackageRelease(
            PackageId.Parse(
                "example-app"),
            SemanticVersion.Parse(
                version),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri(
                    "https://example.invalid/example-app.nupkg"),
                ArtifactDigest.ParseSha256(
                    "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"),
                1024));
    }

    private static MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel
        {
            PackageIdText = "example-app",
            ManifestSource =
                "https://example.invalid/installer-manifest.yml",
            TargetPath = "C:/Apps/Example"
        };
    }


    private sealed class StubStateRepository
        : IInstallStateRepository
    {
        private readonly IReadOnlyList<InstalledPackageState>
            _states;

        private readonly Exception? _listException;

        public StubStateRepository(
            IReadOnlyList<InstalledPackageState> states,
            Exception? listException = null)
        {
            _states = states;
            _listException = listException;
        }

        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<InstalledPackageState?>(
                _states.FirstOrDefault(
                    state =>
                        state.PackageId ==
                        packageId));
        }

        public Task<IReadOnlyList<InstalledPackageState>> ListAsync(
            CancellationToken cancellationToken)
        {
            if (_listException is not null)
            {
                throw _listException;
            }

            return Task.FromResult(
                _states);
        }

        public Task SaveAsync(
            InstalledPackageState state,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubRuntimeService
        : IInstallerRuntimeService
    {
        public Exception? Exception { get; init; }

        public InstallerOperationProgress? Progress { get; init; }

        public InstallerDesktopRequest? LastRequest { get; private set; }

        public PackageId? ResumedPackageId { get; private set; }

        public InstallerUpdateCheckRequest? LastUpdateCheckRequest
        {
            get;
            private set;
        }

        public UpdateCheckResult? UpdateCheckResult
        {
            get;
            init;
        }

        public Exception? UpdateCheckException
        {
            get;
            init;
        }

        public Task<UpdateCheckResult> CheckUpdateAsync(
            InstallerUpdateCheckRequest request,
            CancellationToken cancellationToken)
        {
            LastUpdateCheckRequest = request;

            if (UpdateCheckException is not null)
            {
                throw UpdateCheckException;
            }

            return Task.FromResult(
                UpdateCheckResult
                ?? new UpdateCheckResult(
                    UpdateAvailability.ReleaseNotFound,
                    null,
                    null));
        }

        public Task ExecuteAsync(
            InstallerDesktopRequest request,
            IProgress<InstallerOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (Progress is not null)
            {
                progress.Report(Progress);
            }

            if (Exception is not null)
            {
                throw Exception;
            }

            progress.Report(
                new InstallerOperationProgress(
                    InstallerProgressStage.Completed,
                    100));

            return Task.CompletedTask;
        }

        public Task ResumeAsync(
            PackageId packageId,
            IProgress<InstallerOperationProgress> progress,
            CancellationToken cancellationToken)
        {
            ResumedPackageId = packageId;

            progress.Report(
                new InstallerOperationProgress(
                    InstallerProgressStage.Completed,
                    100));

            return Task.CompletedTask;
        }
    }
}
