// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/JsonLinesInstallerEventLoggerTests.cs
// 📌 Amac: Structured installer event logger JSONL append formatini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Package bazli log dosyasinda her eventin tek JSON satiri olarak kalici yazildigini dogrular
//
// Bagimli Oldugu Katman: Tool

using System.Text.Json;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Operations;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class JsonLinesInstallerEventLoggerTests
{
    [Fact]
    public async Task WriteAsync_AppendsOneJsonObjectPerLine()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstallerLogTests",
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var logger =
                new JsonLinesInstallerEventLogger(
                    new JsonLinesInstallerEventLoggerOptions(
                        root));

            var packageId =
                PackageId.Parse(
                    "example-app");

            var operationId =
                Guid.NewGuid();

            await logger
                .WriteAsync(
                    new InstallerEventEntry(
                        DateTimeOffset.UtcNow,
                        operationId,
                        packageId,
                        InstallerOperationType.Install,
                        InstallerOperationPhase.Started,
                        InstallerEventLevel.Information,
                        "operation.started",
                        "Started",
                        null),
                    CancellationToken.None);

            await logger
                .WriteAsync(
                    new InstallerEventEntry(
                        DateTimeOffset.UtcNow,
                        operationId,
                        packageId,
                        InstallerOperationType.Install,
                        InstallerOperationPhase.Completed,
                        InstallerEventLevel.Information,
                        "operation.completed",
                        "Completed",
                        null),
                    CancellationToken.None);

            var path =
                Path.Combine(
                    root,
                    "example-app.jsonl");

            var lines =
                await File.ReadAllLinesAsync(
                    path);

            Assert.Equal(
                2,
                lines.Length);

            using var first =
                JsonDocument.Parse(
                    lines[0]);

            using var second =
                JsonDocument.Parse(
                    lines[1]);

            Assert.Equal(
                "operation.started",
                first.RootElement
                    .GetProperty("EventName")
                    .GetString());

            Assert.Equal(
                "operation.completed",
                second.RootElement
                    .GetProperty("EventName")
                    .GetString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive: true);
            }
        }
    }
}
