// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/JsonLinesInstallerEventLoggerOptions.cs
// 📌 Amac: Structured installer JSONL log root dizinini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.1.0
// Aciklama: Diagnostic log storage yolunu logger implementationindan ayirir
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed record JsonLinesInstallerEventLoggerOptions
{
    public JsonLinesInstallerEventLoggerOptions(
        string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootDirectory);

        RootDirectory =
            Path.GetFullPath(
                rootDirectory);
    }

    public string RootDirectory { get; }
}
