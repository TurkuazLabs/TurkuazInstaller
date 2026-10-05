// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/tools/CliConsoleReporter.cs
// 📌 Amac: CLI progress ve terminal mesajlarini Console dis dunya adapterina yazar
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Silent modda stdout/stderr cikisini bastirir, normal modda typed progress ve hata metnini yazar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Application.Operations;

namespace TurkuazInstaller.Cli.Tools;

public sealed class CliConsoleReporter
{
    private readonly bool _silent;

    public CliConsoleReporter(
        bool silent)
    {
        _silent = silent;
    }

    public IProgress<InstallerOperationProgress> CreateProgress()
    {
        return new Progress<InstallerOperationProgress>(
            progress =>
            {
                if (_silent)
                {
                    return;
                }

                Console.Out.WriteLine(
                    string.Concat(
                        progress.Stage.ToString(),
                        " ",
                        progress.Percent.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        "%"));
            });
    }

    public void WriteError(
        string message)
    {
        if (_silent)
        {
            return;
        }

        Console.Error.WriteLine(message);
    }
}
