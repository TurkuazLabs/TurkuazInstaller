// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/controllers/Program.cs
// 📌 Amac: Native bootstrap process girisini alir, dependency composition yapar ve runtime servisini cagirir
// 📌 Modul - Controller CSharp
// Version: 1.0.0
// Aciklama: Controller argument requestini parsera aktarir ve Service katmanini Windows launch/self-update Tool adapterlariyla compose eder
//
// Bagimli Oldugu Katman: Controller | Service | Tool | Config

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Bootstrapper.Config;
using TurkuazInstaller.Bootstrapper.Services;
using TurkuazInstaller.Bootstrapper.Tools;
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

            var prerequisiteService =
                new BootstrapPrerequisiteService(
                    environmentProbe,
                    BootstrapDefaults
                        .CreateRequirements());

            var runtimeService =
                new BootstrapRuntimeService(
                    prerequisiteService,
                    new WindowsSelfUpdateHandoff(
                        BootstrapDefaults
                            .CreateSelfUpdateOptions()),
                    new WindowsBootstrapFileCleaner(),
                    new WindowsBootstrapProcessContext(),
                    new WindowsBootstrapApplicationLauncher(),
                    BootstrapDefaults
                        .CreateRuntimeOptions());

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
