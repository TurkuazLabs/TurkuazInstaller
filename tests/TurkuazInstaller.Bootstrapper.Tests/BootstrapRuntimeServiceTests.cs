// 📄 Dosya Yolu: /tests/TurkuazInstaller.Bootstrapper.Tests/BootstrapRuntimeServiceTests.cs
// 📌 Amac: Bootstrap Runtime Service startup, prerequisite, cleanup ve self-update orchestration davranisini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Normal startup'in app/WinUI launch ettigini ve self-update modlarinin dogru Tool portuna yonlendigini dogrular
//
// Bagimli Oldugu Katman: Service | Tool | Config

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Bootstrapper.Config;
using TurkuazInstaller.Bootstrapper.Services;
using TurkuazInstaller.Bootstrapper.Tools;
using TurkuazInstaller.Contracts.Bootstrap;
using Xunit;

namespace TurkuazInstaller.Bootstrapper.Tests;

public sealed class BootstrapRuntimeServiceTests
{
    private const string BootstrapPath =
        @"C:\Bundle\TurkuazInstaller.Bootstrapper.exe";

    private const string ReplacementPath =
        @"C:\Stage\TurkuazInstaller.Bootstrapper.exe";

    [Fact]
    public async Task ExecuteAsync_SupportedEnvironment_LaunchesDesktopFromAppDirectory()
    {
        var handoff =
            new StubSelfUpdateHandoff();

        var cleaner =
            new StubFileCleaner();

        var launcher =
            new StubApplicationLauncher();

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                handoff,
                cleaner,
                launcher);

        var result =
            await service.ExecuteAsync(
                new BootstrapInvocation(
                    null,
                    null,
                    null,
                    new[]
                    {
                        "--product",
                        "example-app"
                    }),
                CancellationToken.None);

        Assert.Equal(
            BootstrapExitCode.Success,
            result);

        Assert.Equal(
            Path.GetFullPath(
                @"C:\Bundle\app\TurkuazInstaller.WinUI.exe"),
            launcher.ExecutablePath);

        Assert.Equal(
            new[]
            {
                "--product",
                "example-app"
            },
            launcher.Arguments);

        Assert.Null(
            handoff.BeginRequest);

        Assert.Null(
            handoff.CompleteRequest);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedArchitecture_DoesNotLaunchDesktop()
    {
        var launcher =
            new StubApplicationLauncher();

        var service =
            CreateService(
                BootstrapCpuArchitecture.Arm64,
                new StubSelfUpdateHandoff(),
                new StubFileCleaner(),
                launcher);

        var result =
            await service.ExecuteAsync(
                new BootstrapInvocation(
                    null,
                    null,
                    null,
                    Array.Empty<string>()),
                CancellationToken.None);

        Assert.Equal(
            BootstrapExitCode.UnsupportedArchitecture,
            result);

        Assert.Null(
            launcher.ExecutablePath);
    }

    [Fact]
    public async Task ExecuteAsync_SelfUpdateStart_InvokesBeginWithoutLaunchingDesktop()
    {
        var handoff =
            new StubSelfUpdateHandoff();

        var launcher =
            new StubApplicationLauncher();

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                handoff,
                new StubFileCleaner(),
                launcher);

        var result =
            await service.ExecuteAsync(
                new BootstrapInvocation(
                    null,
                    null,
                    ReplacementPath,
                    new[]
                    {
                        "--product",
                        "example-app"
                    }),
                CancellationToken.None);

        Assert.Equal(
            BootstrapExitCode.Success,
            result);

        Assert.NotNull(
            handoff.BeginRequest);

        Assert.Equal(
            Path.GetFullPath(
                BootstrapPath),
            handoff.BeginRequest.CurrentExecutablePath);

        Assert.Equal(
            Path.GetFullPath(
                ReplacementPath),
            handoff.BeginRequest.ReplacementExecutablePath);

        Assert.Null(
            launcher.ExecutablePath);
    }

    [Fact]
    public async Task ExecuteAsync_SelfUpdateCompletion_InvokesCompleteOnly()
    {
        var handoff =
            new StubSelfUpdateHandoff();

        var launcher =
            new StubApplicationLauncher();

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                handoff,
                new StubFileCleaner(),
                launcher);

        var complete =
            new SelfUpdateCompleteRequest(
                ReplacementPath,
                BootstrapPath,
                42,
                Array.Empty<string>());

        var result =
            await service.ExecuteAsync(
                new BootstrapInvocation(
                    complete,
                    null,
                    null,
                    Array.Empty<string>()),
                CancellationToken.None);

        Assert.Equal(
            BootstrapExitCode.Success,
            result);

        Assert.Same(
            complete,
            handoff.CompleteRequest);

        Assert.Null(
            launcher.ExecutablePath);
    }

    [Fact]
    public async Task ExecuteAsync_CleanupSource_DeletesBeforeLaunch()
    {
        var cleaner =
            new StubFileCleaner();

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                new StubSelfUpdateHandoff(),
                cleaner,
                new StubApplicationLauncher());

        const string cleanupPath =
            @"C:\Stage\old-bootstrap.exe";

        await service.ExecuteAsync(
            new BootstrapInvocation(
                null,
                cleanupPath,
                null,
                Array.Empty<string>()),
            CancellationToken.None);

        Assert.Equal(
            cleanupPath,
            cleaner.DeletedPath);
    }

    private static BootstrapRuntimeService CreateService(
        BootstrapCpuArchitecture architecture,
        StubSelfUpdateHandoff handoff,
        StubFileCleaner cleaner,
        StubApplicationLauncher launcher)
    {
        var prerequisiteService =
            new BootstrapPrerequisiteService(
                new StubEnvironmentProbe(
                    new BootstrapEnvironmentSnapshot(
                        true,
                        new Version(
                            10,
                            0,
                            26100,
                            0),
                        architecture)),
                new BootstrapRequirements(
                    new Version(
                        10,
                        0,
                        17763,
                        0),
                    new[]
                    {
                        BootstrapCpuArchitecture.X64
                    }));

        return new BootstrapRuntimeService(
            prerequisiteService,
            handoff,
            cleaner,
            new StubProcessContext(
                BootstrapPath),
            launcher,
            new BootstrapRuntimeOptions(
                Path.Combine(
                    "app",
                    "TurkuazInstaller.WinUI.exe")));
    }

    private sealed class StubEnvironmentProbe
        : IBootstrapEnvironmentProbe
    {
        private readonly BootstrapEnvironmentSnapshot _snapshot;

        public StubEnvironmentProbe(
            BootstrapEnvironmentSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public BootstrapEnvironmentSnapshot GetSnapshot()
        {
            return _snapshot;
        }
    }

    private sealed class StubSelfUpdateHandoff
        : ISelfUpdateHandoff
    {
        public SelfUpdateStartRequest? BeginRequest
        {
            get;
            private set;
        }

        public SelfUpdateCompleteRequest? CompleteRequest
        {
            get;
            private set;
        }

        public Task BeginAsync(
            SelfUpdateStartRequest request,
            CancellationToken cancellationToken)
        {
            BeginRequest = request;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(
            SelfUpdateCompleteRequest request,
            CancellationToken cancellationToken)
        {
            CompleteRequest = request;
            return Task.CompletedTask;
        }
    }

    private sealed class StubFileCleaner
        : IBootstrapFileCleaner
    {
        public string? DeletedPath
        {
            get;
            private set;
        }

        public Task TryDeleteAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            DeletedPath = filePath;
            return Task.CompletedTask;
        }
    }

    private sealed class StubProcessContext
        : IBootstrapProcessContext
    {
        private readonly string _path;

        public StubProcessContext(
            string path)
        {
            _path = path;
        }

        public string GetCurrentExecutablePath()
        {
            return _path;
        }
    }

    private sealed class StubApplicationLauncher
        : IBootstrapApplicationLauncher
    {
        public string? ExecutablePath
        {
            get;
            private set;
        }

        public IReadOnlyList<string>? Arguments
        {
            get;
            private set;
        }

        public Task LaunchAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            ExecutablePath =
                executablePath;

            Arguments =
                Array.AsReadOnly(
                    arguments.ToArray());

            return Task.CompletedTask;
        }
    }
}
