// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/controllers/Program.cs
// 📌 Amac: CLI process girisini Controller -> Service zincirine aktarir ve process exit code dondurur
// 📌 Modul - Controller CSharp
// Version: 1.1.0
// Aciklama: Composition root disinda is kurali tutmadan command-line requestini calistirir
//
// Bagimli Oldugu Katman: Controller | Service | Config

using TurkuazInstaller.Cli.Config;
using TurkuazInstaller.Cli.Services;

namespace TurkuazInstaller.Cli.Controllers;

internal static class Program
{
    public static async Task<int> Main(
        string[] args)
    {
        // Diagnostic audit runs without constructing the mutation
        // workflow, package engine, operation journal or resume scheduler.
        if (args.Length > 0 &&
            string.Equals(args[0], "audit", StringComparison.Ordinal))
        {
            return (int)await CliSignedInventoryAuditCommand
                .RunAsync(args, CancellationToken.None)
                .ConfigureAwait(false);
        }

        var controller =
            CliCompositionRoot
                .CreateController();

        var result =
            await controller
                .RunAsync(
                    args,
                    CancellationToken.None)
                .ConfigureAwait(false);

        return (int)result;
    }
}
