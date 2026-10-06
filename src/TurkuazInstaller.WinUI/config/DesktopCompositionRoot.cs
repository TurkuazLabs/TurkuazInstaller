// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopCompositionRoot.cs
// 📌 Amac: WinUI desktop uygulamasinin Controller, Service, Repo, Tool, View ve Language bagimliliklarini tek composition rootta kurar
// 📌 Modul - Config CSharp
// Version: 1.11.0
// Aciklama: Signed trust/proxy/catalog/background/version policy zincirine Windows integration, shared UI catalog ve culture service baglar
//
// Bagimli Oldugu Katman: Controller | Service | Repo | Tool | View | Language

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Prerequisites;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Infrastructure.Artifacts;
using TurkuazInstaller.Infrastructure.Branding;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Operations;
using TurkuazInstaller.Infrastructure.Packages.Velopack;
using TurkuazInstaller.Infrastructure.Processes;
using TurkuazInstaller.Infrastructure.Repositories;
using TurkuazInstaller.Platform.Windows.Config;
using TurkuazInstaller.Platform.Windows.Tools;
using TurkuazInstaller.Presentation.Controllers;
using TurkuazInstaller.Presentation.Language;
using TurkuazInstaller.Presentation.Services;
using TurkuazInstaller.Presentation.ViewModels;
using TurkuazInstaller.WinUI.Services;
using TurkuazInstaller.WinUI.Tools;
using TurkuazInstaller.WinUI.Views;

namespace TurkuazInstaller.WinUI.Config;

internal static class DesktopCompositionRoot
{
    public static MainWindow CreateMainWindow(
        IReadOnlyList<string> startupArguments)
    {
        ArgumentNullException.ThrowIfNull(
            startupArguments);
        var runtimeOptions =
            DesktopPathDefaults.CreateRuntimeOptions();

        var networkProxyOptions =
            new WindowsNetworkProxyOptionsReader(
                WindowsNetworkProxyDefaults
                    .CreateConfigPath())
                .Read();

        var httpClient =
            WindowsHttpClientFactory.Create(
                networkProxyOptions);

        var backgroundUpdatePolicy =
            new WindowsBackgroundUpdatePolicyReader(
                WindowsBackgroundUpdateDefaults
                    .CreateConfigPath())
                .Read();

        var versionPolicyRepository =
            new WindowsVersionUpdatePolicyRepository(
                WindowsVersionUpdatePolicyDefaults
                    .CreateConfigPath());

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

        var resumeRequestRepository =
            new JsonInstallerResumeRequestRepository(
                new JsonInstallerResumeRequestRepositoryOptions(
                    runtimeOptions.ResumeRoot));

        var rebootResumeScheduler =
            new WindowsRunOnceRebootResumeScheduler(
                new WindowsRegistryRunOnceStore(),
                new WindowsRunOnceRebootResumeSchedulerOptions(
                    runtimeOptions.BootstrapExecutablePath,
                    runtimeOptions.RebootResumeValueNamePrefix));

        var processRunner =
            new SystemProcessRunner();

        var windowsIntegrationManager =
            new WindowsIntegrationManager(
                new WindowsIntegrationReceiptStore(
                    runtimeOptions.IntegrationRoot),
                new WindowsShellLinkShortcutStore(),
                new WindowsRegistryProtocolRegistrationStore());

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
                prerequisiteInstaller,
                rebootResumeScheduler,
                versionPolicyRepository,
                windowsIntegrationManager);

        var runtimeService =
            new WinUiInstallerRuntimeService(
                new ManifestReleaseProviderFactory(
                    httpClient,
                    manifestReader,
                    manifestSignatureVerifier),
                workflowService,
                stateRepository,
                operationJournal,
                resumeRequestRepository,
                runtimeOptions,
                versionPolicyRepository);

        var uiProfile =
            new YamlInstallerUiProfileRepository(
                runtimeOptions.UiProfilePath)
                .Read();

        var uiCatalog =
            new InstallerUiCatalog(
                uiProfile);

        new InstallerUiCultureService()
            .Apply(
                uiCatalog);

        var viewModel =
            new MainWindowViewModel(
                uiCatalog);

        var backgroundUpdateService =
            new BackgroundUpdateService(
                runtimeService,
                viewModel,
                backgroundUpdatePolicy,
                uiCatalog);

        var desktopService =
            new InstallerDesktopService(
                viewModel,
                runtimeService,
                new InstallerResumeLaunchParser(),
                new InstalledAppCatalogService(
                    stateRepository,
                    uiCatalog),
                backgroundUpdateService,
                uiCatalog);

        var controller =
            new MainWindowController(
                desktopService);

        return new MainWindow(
            viewModel,
            controller,
            desktopService,
            startupArguments);
    }
}
