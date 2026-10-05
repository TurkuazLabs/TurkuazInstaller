// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/config/CliCompositionRoot.cs
// 📌 Amac: CLI Controller, Service, Repo ve Windows Tool bagimliliklarini tek composition rootta kurar
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: WinUI ile ayni trust/state/journal zincirini GUI bagimliligi olmadan silent runtime'a baglar
//
// Bagimli Oldugu Katman: Controller | Service | Repo | Tool | Config

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Application.Prerequisites;
using TurkuazInstaller.Cli.Controllers;
using TurkuazInstaller.Cli.Services;
using TurkuazInstaller.Cli.Tools;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Infrastructure.Artifacts;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Operations;
using TurkuazInstaller.Infrastructure.Packages.Velopack;
using TurkuazInstaller.Infrastructure.Processes;
using TurkuazInstaller.Infrastructure.Repositories;
using TurkuazInstaller.Platform.Windows.Tools;

namespace TurkuazInstaller.Cli.Config;

internal static class CliCompositionRoot
{
    public static CliController CreateController()
    {
        var options =
            CliPathDefaults.CreateRuntimeOptions();

        var httpClient =
            new HttpClient();

        var manifestReader =
            new InstallerManifestReader();

        var manifestTrustRepository =
            new YamlManifestTrustPolicyRepository(
                new YamlManifestTrustPolicyRepositoryOptions(
                    options.ManifestTrustStorePath));

        var manifestSignatureVerifier =
            new CmsManifestSignatureVerifier(
                manifestTrustRepository);

        var stateRepository =
            new JsonInstallStateRepository(
                new JsonInstallStateRepositoryOptions(
                    options.StateRoot));

        var operationJournal =
            new JsonOperationJournalRepository(
                new JsonOperationJournalRepositoryOptions(
                    options.JournalRoot));

        var resumeRepository =
            new JsonInstallerResumeRequestRepository(
                new JsonInstallerResumeRequestRepositoryOptions(
                    options.ResumeRoot));

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

        var rebootScheduler =
            new CliRunOnceRebootResumeScheduler(
                new WindowsRegistryRunOnceStore(),
                options.ExecutablePath,
                options.RebootResumeValueNamePrefix);

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
                new FileInstallerOperationLock(
                    new FileInstallerOperationLockOptions(
                        options.LockRoot)),
                operationJournal,
                new JsonLinesInstallerEventLogger(
                    new JsonLinesInstallerEventLoggerOptions(
                        options.LogRoot)),
                new WindowsPrerequisiteInstaller(
                    processRunner,
                    new WindowsElevatedProcessRunner()),
                rebootScheduler);

        var runtimeService =
            new CliInstallerRuntimeService(
                new CliManifestReleaseProviderFactory(
                    httpClient,
                    manifestReader,
                    manifestSignatureVerifier),
                workflowService,
                stateRepository,
                operationJournal,
                resumeRepository,
                options);

        return new CliController(
            new CliService(
                new CliCommandParser(),
                runtimeService));
    }
}
