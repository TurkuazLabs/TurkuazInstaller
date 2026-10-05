// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/InstallerDesktopServiceTests.cs
// 📌 Amac: InstallerDesktopService progress, success ve recovery state davranisini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Progress event sirasi ve terminal completed state'inin gec callback ile geriye sarilmadigini framework bagimsiz test eder
//
// Bagimli Oldugu Katman: Service | View

using TurkuazInstaller.Application.Operations;
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
            runtime);

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
            runtime);

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
            runtime);

        await service.RunAsync(
            InstallerOperationKind.Repair);

        Assert.Equal(100, viewModel.ProgressValue);
        Assert.Equal(
            InstallerUiLabels.Completed,
            viewModel.StatusMessage);
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

    private sealed class StubRuntimeService
        : IInstallerRuntimeService
    {
        public Exception? Exception { get; init; }

        public InstallerOperationProgress? Progress { get; init; }

        public Task ExecuteAsync(
            InstallerDesktopRequest request,
            IProgress<InstallerOperationProgress> progress,
            CancellationToken cancellationToken)
        {
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
    }
}
