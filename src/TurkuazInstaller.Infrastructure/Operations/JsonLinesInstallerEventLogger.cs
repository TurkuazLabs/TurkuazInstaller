// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/JsonLinesInstallerEventLogger.cs
// 📌 Amac: Installer structured diagnostic eventlerini package bazli JSONL dosyalarina append eder
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: Her satiri bagimsiz JSON event olarak yazar; package operation lock ayni package logunda cross-process cakismayi engeller
//
// Bagimli Oldugu Katman: Tool | Service

using System.Text;
using System.Text.Json;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Operations;

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed class JsonLinesInstallerEventLogger
    : IInstallerEventLogger
{
    private const string LogExtension = ".jsonl";

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            WriteIndented = false
        };

    private readonly JsonLinesInstallerEventLoggerOptions _options;

    public JsonLinesInstallerEventLogger(
        JsonLinesInstallerEventLoggerOptions options)
    {
        _options = options;
    }

    public async Task WriteAsync(
        InstallerEventEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Directory.CreateDirectory(
            _options.RootDirectory);

        var path =
            Path.Combine(
                _options.RootDirectory,
                string.Concat(
                    entry.PackageId.Value,
                    LogExtension));

        var line =
            string.Concat(
                JsonSerializer.Serialize(
                    entry,
                    SerializerOptions),
                Environment.NewLine);

        var bytes =
            Encoding.UTF8.GetBytes(
                line);

        await using var stream =
            new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous);

        await stream
            .WriteAsync(
                bytes,
                cancellationToken)
            .ConfigureAwait(false);

        await stream
            .FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }
}
