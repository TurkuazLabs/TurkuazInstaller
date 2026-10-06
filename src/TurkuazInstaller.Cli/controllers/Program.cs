// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/controllers/Program.cs
// 📌 Amac: CLI process girisini Controller -> Service zincirine aktarir ve process exit code dondurur
// 📌 Modul - Controller CSharp
// Version: 1.0.0
// Aciklama: Composition root disinda is kurali tutmadan command-line requestini calistirir
//
// Bagimli Oldugu Katman: Controller | Service | Config

using TurkuazInstaller.Cli.Config;

namespace TurkuazInstaller.Cli.Controllers;

internal static class Program
{
    public static async Task<int> Main(
        string[] args)
    {
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
