// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/config/DesktopCompositionRoot.cs
// 📌 Amac: WinUI desktop uygulamasinin Controller, Service, Repo, Tool, View ve Language bagimliliklarini tek composition rootta kurar
// 📌 Modul - Config CSharp
// Version: 1.18.0
// Aciklama: Theme trust bilgisini user YAML yerine compile-time allowlist'ten getirir
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
        IReadOnlyList<string> startupArguments,
        IThemeDecryptionKeyProvider? authorizedThemeKeys = null)
    {
        ArgumentNullException.ThrowIfNull(
            startupArguments);
        var runtimeOptions =
            DesktopPathDefaults.CreateRuntimeOptions();

        var compiledProduct = CompiledThemePublisherPins.RequiredThemePackageId;
        var requiredProductId = compiledProduct is null
            ? null
            : TurkuazInstaller.Domain.Products.PackageId.Parse(compiledProduct);
        string? approvedManifestUrl = null;



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

        // Generic community mode retains mutable update-manifest trust.
        // Branded builds MUST compile both manifest source and release signer.
        // Missing pins deliberately stop startup instead of opening an
        // unauthorized manifest via writable user configuration.
        TurkuazInstaller.Contracts.Manifests.IManifestTrustPolicyRepository
            manifestTrustRepository;
        if (requiredProductId is null)
        {
            manifestTrustRepository = new YamlManifestTrustPolicyRepository(
                new YamlManifestTrustPolicyRepositoryOptions(
                    runtimeOptions.ManifestTrustStorePath));
        }
        else
        {
            var approvedRelease =
                CompiledBrandedReleasePolicy.Create(requiredProductId);
            manifestTrustRepository = approvedRelease.Trust;
            approvedManifestUrl = approvedRelease.ManifestUrl;
        }

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

        // Product identity is compiled into signed branded releases; user
        // YAML can neither disable nor switch the mutation target.
        if (requiredProductId is not null)
        {
            viewModel.PackageIdText = requiredProductId.Value;
            viewModel.ManifestSource = approvedManifestUrl!;
        }

        var desktopService =
            new InstallerDesktopService(
                viewModel,
                runtimeService,
                new InstallerResumeLaunchParser(),
                new InstalledAppCatalogService(
                    stateRepository,
                    uiCatalog),
                backgroundUpdateService,
                uiCatalog,
                requiredProductId,
                approvedManifestUrl);

        var controller =
            new MainWindowController(
                desktopService);

        // An explicitly product-bound theme is mandatory when configured.
        // A theme.tpk may not supply its own trust anchor.
        var requestedProduct = uiProfile.Branding.ThemePackageId;
        if (compiledProduct is not null && requestedProduct is not null &&
            !string.Equals(compiledProduct, requestedProduct, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "UI profile may not override the compiled required theme identity.");
        }

        // A required branded theme must remain required even when a local
        // user deletes or rewrites the optional UI profile.
        var themePackageId = (compiledProduct ?? requestedProduct) is { } id
            ? TurkuazInstaller.Domain.Products.PackageId.Parse(id)
            : null;

        // A branded build must pin its exact signed theme ID and version.
        // A user-writable YAML file cannot approve another signed release.
        var exactRelease = compiledProduct is null
            ? null
            : PinnedThemeRelease.Create(
                CompiledThemePublisherPins.RequiredThemeId ??
                    throw new InvalidOperationException("Compiled theme identity is missing."),
                CompiledThemePublisherPins.RequiredThemeVersion ??
                    throw new InvalidOperationException("Compiled theme version is missing."));

        // Both transport types verify the same CMS-signed inner manifest
        // with the same code-pinned publisher. Transport policy comes from
        // reviewed application source, never from user YAML or theme.tpe.
        var themeSource = themePackageId is null
            ? null
            : VerifiedThemePackSourceFactory.Create(
                CompiledThemePublisherPins.TransportMode,
                new SignedThemePackReader(
                    new CmsManifestSignatureVerifier(CompiledThemePublisherPins.Create())),
                authorizedThemeKeys,
                exactRelease);

        return new MainWindow(
            viewModel,
            controller,
            desktopService,
            startupArguments,
            themeSource?.Reader,
            themePackageId,
            themeSource is null ? null
                : Path.Combine(AppContext.BaseDirectory, themeSource.FileName),
            requiredProductId is not null);
    }
}
