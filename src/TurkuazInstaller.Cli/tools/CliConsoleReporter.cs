// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/tools/CliConsoleReporter.cs
// 📌 Amac: CLI progress ve terminal mesajlarini Console dis dunya adapterina yazar
// 📌 Modul - Tool CSharp
// Version: 1.0.1
// Aciklama: Silent modda cikisi bastirir, normal modda progress'i senkron yazar ve process kapanisinda event kaybini onler
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
        return new ConsoleProgress(
            _silent);
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

    private sealed class ConsoleProgress
        : IProgress<InstallerOperationProgress>
    {
        private readonly bool _silent;

        public ConsoleProgress(
            bool silent)
        {
            _silent = silent;
        }

        public void Report(
            InstallerOperationProgress value)
        {
            ArgumentNullException.ThrowIfNull(
                value);

            if (_silent)
            {
                return;
            }

            Console.Out.WriteLine(
                string.Concat(
                    value.Stage.ToString(),
                    " ",
                    value.Percent.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    "%"));
        }
    }
}
