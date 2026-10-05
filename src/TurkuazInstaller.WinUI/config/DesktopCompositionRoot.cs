// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopCompositionRoot.cs
// 📌 Amac: WinUI desktop uygulamasinin Controller, Service, Repo, Tool, View ve Language bagimliliklarini tek composition rootta kurar
// 📌 Modul - Config CSharp
// Version: 1.3.0
// Aciklama: Detached trust, generic prerequisite detector registry, auto-install Tool, package lock, journal, log ve Velopack runtime adapterlarini baglar
//
// Bagimli Oldugu Katman: Controller | Service | Repo | Tool | View | Language

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Prerequisites;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Infrastructure.Artifacts;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Operations;
using TurkuazInstaller.Infrastructure.Packages.Velopack;
using TurkuazInstaller.Infrastructure.Processes;
using TurkuazInstaller.Infrastructure.Repositories;
using TurkuazInstaller.Platform.Windows.Tools;
using TurkuazInstaller.Presentation.Controllers;
using TurkuazInstaller.Presentation.Services;
using TurkuazInstaller.Presentation.ViewModels;
using TurkuazInstaller.WinUI.Services;
using TurkuazInstaller.WinUI.Tools;
using TurkuazInstaller.WinUI.Views;

namespace TurkuazInstaller.WinUI.Config;

internal static class DesktopCompositionRoot
{
    public static MainWindow CreateMainWindow()
    {
        var runtimeOptions =
            DesktopPathDefaults.CreateRuntimeOptions();

        var httpClient =
            new HttpClient();

        var manifestReader =
            new InstallerManifestReader();

        var manifestTrustRepository =
            new YamlManifestTrustPolicyRepository(
                new YamlManifestTrustPolicyRepositoryOptions(
                    runtimeOptions.ManifestTrustStorePath));

        var manifestSignatureVerifier =
            new CmsManifestSignatureVerifier(
                manifestTrustRepository);

        var stateRepository =
            new JsonInstallStateRepository(
                new JsonInstallStateRepositoryOptions(
                    runtimeOptions.StateRoot));

        var operationLock =
            new FileInstallerOperationLock(
                new FileInstallerOperationLockOptions(
                    runtimeOptions.LockRoot));

        var operationJournal =
            new JsonOperationJournalRepository(
                new JsonOperationJournalRepositoryOptions(
                    runtimeOptions.JournalRoot));

        var eventLogger =
            new JsonLinesInstallerEventLogger(
                new JsonLinesInstallerEventLoggerOptions(
                    runtimeOptions.LogRoot));

        var processRunner =
            new SystemProcessRunner();

        var prerequisiteProbe =
            new PrerequisiteDetectionService(
                new IPrerequisiteDetector[]
                {
                    new WindowsBuildPrerequisiteDetector(),
                    new WindowsArchitecturePrerequisiteDetector(),
                    new WindowsDesktopRuntimePrerequisiteDetector()
                });

        var prerequisiteInstaller =
            new WindowsPrerequisiteInstaller(
                processRunner,
                new WindowsElevatedProcessRunner());

        var workflowService =
            new InstallerWorkflowService(
                new DefaultArtifactDownloader(
                    httpClient),
                new Sha256ArtifactVerifier(),
                new VelopackPackageEngine(
                    processRunner),
                stateRepository,
                new WindowsAuthenticodeArtifactSignatureVerifier(),
                prerequisiteProbe,
                operationLock,
                operationJournal,
                eventLogger,
                prerequisiteInstaller);

        var runtimeService =
            new WinUiInstallerRuntimeService(
                new ManifestReleaseProviderFactory(
                    httpClient,
                    manifestReader,
                    manifestSignatureVerifier),
                workflowService,
                stateRepository,
                runtimeOptions);

        var viewModel =
            new MainWindowViewModel();

        var desktopService =
            new InstallerDesktopService(
                viewModel,
                runtimeService);

        var controller =
            new MainWindowController(
                desktopService);

        return new MainWindow(
            viewModel,
            controller,
            desktopService);
    }
}
