// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/controllers/CliController.cs
// 📌 Amac: Process command-line requestini alir ve yalniz CliService cagrisina donusturur
// 📌 Modul - Controller CSharp
// Version: 1.0.0
// Aciklama: Controller business logic tutmadan raw argument requestini Service katmanina aktarir
//
// Bagimli Oldugu Katman: Controller | Service

using TurkuazInstaller.Cli.Services;

namespace TurkuazInstaller.Cli.Controllers;

internal sealed class CliController
{
    private readonly CliService _service;

    public CliController(
        CliService service)
    {
        _service = service;
    }

    public Task<CliExitCode> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        return _service.RunAsync(
            arguments,
            cancellationToken);
    }
}
