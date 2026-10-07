// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/controllers/Program.cs
// 📌 Amac: Native bootstrap process girisini alir, dependency composition yapar ve runtime servisini cagirir
// 📌 Modul - Controller CSharp
// Version: 1.3.0
// Aciklama: Controller argument requestini parsera aktarir; process architecture-aware self-update ve ortak proxy politikasini compose eder
//
// Bagimli Oldugu Katman: Controller | Service | Tool | Config

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Bootstrapper.Config;
using TurkuazInstaller.Bootstrapper.Services;
using TurkuazInstaller.Bootstrapper.Tools;
using TurkuazInstaller.Platform.Windows.Config;
using TurkuazInstaller.Platform.Windows.Tools;

namespace TurkuazInstaller.Bootstrapper.Controllers;

internal static class Program
{
    public static async Task<int> Main(
        string[] args)
    {
        try
        {
            var parser =
                new BootstrapCommandParser();

            var invocation =
                parser.Parse(args);

            var environmentProbe =
                new WindowsBootstrapEnvironmentProbe();

            var environmentSnapshot =
                environmentProbe.GetSnapshot();

            var prerequisiteService =
                new BootstrapPrerequisiteService(
                    environmentProbe,
                    BootstrapDefaults
                        .CreateRequirements());

            var selfUpdateOptions =
                BootstrapDefaults
                    .CreateSelfUpdateDiscoveryOptions(
                        environmentSnapshot.Architecture);

            var networkProxyOptions =
                new WindowsNetworkProxyOptionsReader(
                    WindowsNetworkProxyDefaults
                        .CreateConfigPath())
                    .Read();

            using var discoveryHttpClient =
                WindowsHttpClientFactory.Create(
                    networkProxyOptions,
                    selfUpdateOptions
                        .DiscoveryTimeout);

            using var downloadHttpClient =
                WindowsHttpClientFactory.Create(
                    networkProxyOptions,
                    selfUpdateOptions
                        .DownloadTimeout);

            var runtimeService =
                new BootstrapRuntimeService(
                    prerequisiteService,
                    new WindowsSelfUpdateHandoff(
                        BootstrapDefaults
                            .CreateSelfUpdateOptions()),
                    new GitHubBootstrapSelfUpdateDiscovery(
                        discoveryHttpClient,
                        selfUpdateOptions),
                    new HttpBootstrapSelfUpdateDownloader(
                        downloadHttpClient),
                    new WindowsBootstrapSelfUpdateTrustVerifier(),
                    new WindowsBootstrapFileCleaner(),
                    new WindowsBootstrapProcessContext(),
                    new WindowsBootstrapApplicationLauncher(),
                    BootstrapDefaults
                        .CreateRuntimeOptions(),
                    selfUpdateOptions);

            var exitCode =
                await runtimeService
                    .ExecuteAsync(
                        invocation,
                        CancellationToken.None)
                    .ConfigureAwait(false);

            return (int)exitCode;
        }
        catch (FormatException)
        {
            return (int)
                BootstrapExitCode.InvalidInvocation;
        }
        catch (Exception)
        {
            return (int)
                BootstrapExitCode.RuntimeFailure;
        }
    }
}
