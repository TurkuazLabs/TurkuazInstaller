// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/InstallerDesktopServiceTests.cs
// 📌 Amac: InstallerDesktopService progress, success ve recovery state davranisini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.2.0
// Aciklama: Progress/reboot resume davranisina ek olarak startup catalog refresh ve catalog error ayrimini framework bagimsiz test eder
//
// Bagimli Oldugu Katman: Service | View

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.State;
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
