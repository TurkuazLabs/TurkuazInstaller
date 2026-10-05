// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsRunOnceRebootResumeSchedulerTests.cs
// 📌 Amac: Reboot resume scheduler package-scoped RunOnce komutunu unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: ! prefix, bootstrap path, internal resume argumani ve cancel davranisini Registry kullanmadan test eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsRunOnceRebootResumeSchedulerTests
{
    [Fact]
    public async Task ScheduleAsync_WritesPackageScopedDeferredRunOnceCommand()
    {
        var store =
            new StubRunOnceStore();

        var bootstrapPath =
            CreateTemporaryBootstrap();

        try
        {
            var scheduler =
                new WindowsRunOnceRebootResumeScheduler(
                    store,
                    new WindowsRunOnceRebootResumeSchedulerOptions(
                        bootstrapPath,
                        "TurkuazInstaller.Resume"));

            await scheduler.ScheduleAsync(
                PackageId.Parse(
                    "example-app"),
                CancellationToken.None);

            Assert.Equal(
                "!TurkuazInstaller.Resume.example-app",
                store.ValueName);

            Assert.Equal(
                string.Concat(
                    "\"",
                    bootstrapPath,
                    "\" ",
                    InstallerResumeLaunchArguments.PackageOption,
                    " example-app"),
                store.CommandLine);
        }
        finally
        {
            File.Delete(
                bootstrapPath);
        }
    }

    [Fact]
    public async Task CancelAsync_DeletesSamePackageScopedValue()
    {
        var store =
            new StubRunOnceStore();

        var bootstrapPath =
            CreateTemporaryBootstrap();

        try
        {
            var scheduler =
                new WindowsRunOnceRebootResumeScheduler(
                    store,
                    new WindowsRunOnceRebootResumeSchedulerOptions(
                        bootstrapPath,
                        "TurkuazInstaller.Resume"));

            await scheduler.CancelAsync(
                PackageId.Parse(
                    "example-app"),
                CancellationToken.None);

            Assert.Equal(
                "!TurkuazInstaller.Resume.example-app",
                store.DeletedValueName);
        }
        finally
        {
            File.Delete(
                bootstrapPath);
        }
    }

    private static string CreateTemporaryBootstrap()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "TurkuazInstaller.Bootstrapper.exe");

        File.WriteAllBytes(
            path,
            Array.Empty<byte>());

        return path;
    }

    private sealed class StubRunOnceStore
        : IWindowsRunOnceStore
    {
        public string? ValueName { get; private set; }

        public string? CommandLine { get; private set; }

        public string? DeletedValueName { get; private set; }

        public void Set(
            string valueName,
            string commandLine)
        {
            ValueName = valueName;
            CommandLine = commandLine;
        }

        public void Delete(
            string valueName)
        {
            DeletedValueName = valueName;
        }
    }
}
