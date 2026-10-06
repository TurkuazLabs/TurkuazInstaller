// 📄 Dosya Yolu: /tests/TurkuazInstaller.Bootstrapper.Tests/BootstrapRuntimeServiceTests.cs
// 📌 Amac: Bootstrap Runtime Service startup, prerequisite, cleanup ve trusted self-update orchestration davranisini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.3.0
// Aciklama: Normal launch ve explicit fail-closed trust yaninda automatic self-update failure fallback, cleanup ve cancellation davranisini dogrular
//
// Bagimli Oldugu Katman: Service | Tool | Config

using TurkuazInstaller.Application.Bootstrap;
using TurkuazInstaller.Bootstrapper.Config;
using TurkuazInstaller.Bootstrapper.Services;
using TurkuazInstaller.Bootstrapper.Tools;
using TurkuazInstaller.Contracts.Bootstrap;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.Verification;
using Xunit;

namespace TurkuazInstaller.Bootstrapper.Tests;

public sealed class BootstrapRuntimeServiceTests
{
    private const string BootstrapPath =
        @"C:\Bundle\TurkuazInstaller.Bootstrapper.exe";

    private const string ReplacementPath =
        @"C:\Stage\TurkuazInstaller.Bootstrapper.exe";

    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

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
    public async Task ExecuteAsync_DiscoveredUpdate_VerifiesAndBeginsHandoffWithoutLaunchingDesktop()
    {
        var release =
            CreateUpdateRelease();

        var discovery =
            new StubSelfUpdateDiscovery(
                release);

        var downloader =
            new StubSelfUpdateDownloader(
                ReplacementPath);

        var trustVerifier =
            new StubSelfUpdateTrustVerifier
            {
                CanSelfUpdate = true
            };

        var handoff =
            new StubSelfUpdateHandoff();

        var launcher =
            new StubApplicationLauncher();

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                handoff,
                new StubFileCleaner(),
                launcher,
                discovery,
                downloader,
                trustVerifier);

        var result =
            await service.ExecuteAsync(
                new BootstrapInvocation(
                    null,
                    null,
                    null,
                    new[]
                    {
                        "--resume-package",
                        "example-app"
                    }),
                CancellationToken.None);

        Assert.Equal(
            BootstrapExitCode.Success,
            result);

        Assert.Equal(
            SemanticVersion.Parse(
                "1.0.0"),
            discovery.CurrentVersion);

        Assert.Same(
            release,
            downloader.Release);

        Assert.Equal(
            BootstrapPath,
            trustVerifier.CurrentExecutablePath);

        Assert.Equal(
            ReplacementPath,
            trustVerifier.ReplacementExecutablePath);

        Assert.NotNull(
            handoff.BeginRequest);

        Assert.Equal(
            Path.GetFullPath(
                ReplacementPath),
            handoff.BeginRequest!.ReplacementExecutablePath);

        Assert.Equal(
            new[]
            {
                "--resume-package",
                "example-app"
            },
            handoff.BeginRequest.ResumeArguments);

        Assert.Null(
            launcher.ExecutablePath);
    }

    [Fact]
    public async Task ExecuteAsync_AutomaticDiscoveryFailure_LaunchesCurrentDesktop()
    {
        var launcher =
            new StubApplicationLauncher();

        var discovery =
            new StubSelfUpdateDiscovery
            {
                Exception =
                    new InvalidDataException(
                        "Malformed latest release metadata.")
            };

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                new StubSelfUpdateHandoff(),
                new StubFileCleaner(),
                launcher,
                discovery,
                trustVerifier:
                    new StubSelfUpdateTrustVerifier
                    {
                        CanSelfUpdate = true
                    });

        var result =
            await service.ExecuteAsync(
                new BootstrapInvocation(
                    null,
                    null,
                    null,
                    Array.Empty<string>()),
                CancellationToken.None);

        Assert.Equal(
            BootstrapExitCode.Success,
            result);
        Assert.Equal(
            Path.GetFullPath(
                @"C:\Bundle\app\TurkuazInstaller.WinUI.exe"),
            launcher.ExecutablePath);
    }

    [Fact]
    public async Task ExecuteAsync_AutomaticUntrustedReplacement_CleansStageAndLaunchesCurrentDesktop()
    {
        var cleaner =
            new StubFileCleaner();

        var launcher =
            new StubApplicationLauncher();

        var handoff =
            new StubSelfUpdateHandoff();

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                handoff,
                cleaner,
                launcher,
                new StubSelfUpdateDiscovery(
                    CreateUpdateRelease()),
                new StubSelfUpdateDownloader(
                    ReplacementPath),
                new StubSelfUpdateTrustVerifier
                {
                    CanSelfUpdate = true,
                    Verification =
                        VerificationResult.Failed(
                            VerificationFailure.SignatureInvalid,
                            "Signer mismatch.")
                });

        var result =
            await service.ExecuteAsync(
                new BootstrapInvocation(
                    null,
                    null,
                    null,
                    Array.Empty<string>()),
                CancellationToken.None);

        Assert.Equal(
            BootstrapExitCode.Success,
            result);
        Assert.Equal(
            ReplacementPath,
            cleaner.DeletedPath);
        Assert.Null(
            handoff.BeginRequest);
        Assert.Equal(
            Path.GetFullPath(
                @"C:\Bundle\app\TurkuazInstaller.WinUI.exe"),
            launcher.ExecutablePath);
    }

    [Fact]
    public async Task ExecuteAsync_AutomaticUpdateCancellation_PropagatesCancellation()
    {
        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        var discovery =
            new StubSelfUpdateDiscovery
            {
                Exception =
                    new OperationCanceledException(
                        cancellation.Token)
            };

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                new StubSelfUpdateHandoff(),
                new StubFileCleaner(),
                new StubApplicationLauncher(),
                discovery,
                trustVerifier:
                    new StubSelfUpdateTrustVerifier
                    {
                        CanSelfUpdate = true
                    });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ExecuteAsync(
                new BootstrapInvocation(
                    null,
                    null,
                    null,
                    Array.Empty<string>()),
                cancellation.Token));
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
    public async Task ExecuteAsync_SelfUpdateStart_VerifiesThenInvokesBeginWithoutLaunchingDesktop()
    {
        var handoff =
            new StubSelfUpdateHandoff();

        var launcher =
            new StubApplicationLauncher();

        var trustVerifier =
            new StubSelfUpdateTrustVerifier();

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                handoff,
                new StubFileCleaner(),
                launcher,
                trustVerifier:
                    trustVerifier);

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

        Assert.Equal(
            BootstrapPath,
            trustVerifier.CurrentExecutablePath);

        Assert.Equal(
            Path.GetFullPath(
                ReplacementPath),
            trustVerifier.ReplacementExecutablePath);

        Assert.NotNull(
            handoff.BeginRequest);

        Assert.Equal(
            Path.GetFullPath(
                BootstrapPath),
            handoff.BeginRequest!.CurrentExecutablePath);

        Assert.Equal(
            Path.GetFullPath(
                ReplacementPath),
            handoff.BeginRequest.ReplacementExecutablePath);

        Assert.Null(
            launcher.ExecutablePath);
    }

    [Fact]
    public async Task ExecuteAsync_SelfUpdateStart_UntrustedReplacementDoesNotBegin()
    {
        var handoff =
            new StubSelfUpdateHandoff();

        var trustVerifier =
            new StubSelfUpdateTrustVerifier
            {
                Verification =
                    VerificationResult.Failed(
                        VerificationFailure.SignatureInvalid,
                        "Signer mismatch.")
            };

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                handoff,
                new StubFileCleaner(),
                new StubApplicationLauncher(),
                trustVerifier:
                    trustVerifier);

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                service.ExecuteAsync(
                    new BootstrapInvocation(
                        null,
                        null,
                        ReplacementPath,
                        Array.Empty<string>()),
                    CancellationToken.None));

        Assert.Null(
            handoff.BeginRequest);
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
    public async Task ExecuteAsync_SelfUpdateCompletion_UntrustedSourceDoesNotComplete()
    {
        var handoff =
            new StubSelfUpdateHandoff();

        var trustVerifier =
            new StubSelfUpdateTrustVerifier
            {
                Verification =
                    VerificationResult.Failed(
                        VerificationFailure.SignatureInvalid,
                        "Signer mismatch.")
            };

        var service =
            CreateService(
                BootstrapCpuArchitecture.X64,
                handoff,
                new StubFileCleaner(),
                new StubApplicationLauncher(),
                trustVerifier:
                    trustVerifier);

        var complete =
            new SelfUpdateCompleteRequest(
                ReplacementPath,
                BootstrapPath,
                42,
                Array.Empty<string>());

        await Assert.ThrowsAsync<InvalidDataException>(
            () =>
                service.ExecuteAsync(
                    new BootstrapInvocation(
                        complete,
                        null,
                        null,
                        Array.Empty<string>()),
                    CancellationToken.None));

        Assert.Null(
            handoff.CompleteRequest);
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
        StubApplicationLauncher launcher,
        StubSelfUpdateDiscovery? discovery = null,
        StubSelfUpdateDownloader? downloader = null,
        StubSelfUpdateTrustVerifier? trustVerifier = null)
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
            discovery
                ?? new StubSelfUpdateDiscovery(),
            downloader
                ?? new StubSelfUpdateDownloader(
                    ReplacementPath),
            trustVerifier
                ?? new StubSelfUpdateTrustVerifier(),
            cleaner,
            new StubProcessContext(
                BootstrapPath),
            launcher,
            new BootstrapRuntimeOptions(
                Path.Combine(
                    "app",
                    "TurkuazInstaller.WinUI.exe")),
            new BootstrapSelfUpdateOptions(
                SemanticVersion.Parse(
                    "1.0.0"),
                new Uri(
                    "https://api.github.com/repos/TurkuazLabs/TurkuazInstaller/releases/latest"),
                "TurkuazInstaller.Bootstrapper.exe",
                @"C:\Stage",
                TimeSpan.FromSeconds(
                    5),
                TimeSpan.FromMinutes(
                    2)));
    }

    private static BootstrapSelfUpdateRelease
        CreateUpdateRelease()
    {
        return new BootstrapSelfUpdateRelease(
            SemanticVersion.Parse(
                "1.1.0"),
            "TurkuazInstaller.Bootstrapper.exe",
            new Uri(
                "https://github.com/TurkuazLabs/TurkuazInstaller/releases/download/v1.1.0/TurkuazInstaller.Bootstrapper.exe"),
            ArtifactDigest.ParseSha256(
                Digest),
            1024);
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

    private sealed class StubSelfUpdateDiscovery
        : IBootstrapSelfUpdateDiscovery
    {
        private readonly BootstrapSelfUpdateRelease? _release;

        public StubSelfUpdateDiscovery(
            BootstrapSelfUpdateRelease? release = null)
        {
            _release = release;
        }

        public SemanticVersion? CurrentVersion
        {
            get;
            private set;
        }

        public Exception? Exception
        {
            get;
            init;
        }

        public Task<BootstrapSelfUpdateRelease?> GetLatestAsync(
            SemanticVersion currentVersion,
            CancellationToken cancellationToken)
        {
            CurrentVersion = currentVersion;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(
                _release);
        }
    }

    private sealed class StubSelfUpdateDownloader
        : IBootstrapSelfUpdateDownloader
    {
        private readonly string _path;

        public StubSelfUpdateDownloader(
            string path)
        {
            _path = path;
        }

        public BootstrapSelfUpdateRelease? Release
        {
            get;
            private set;
        }

        public Task<string> DownloadAsync(
            BootstrapSelfUpdateRelease release,
            string stagingRoot,
            CancellationToken cancellationToken)
        {
            Release = release;

            return Task.FromResult(
                _path);
        }
    }

    private sealed class StubSelfUpdateTrustVerifier
        : IBootstrapSelfUpdateTrustVerifier
    {
        public bool CanSelfUpdate { get; init; }

        public VerificationResult Verification { get; init; } =
            VerificationResult.Passed();

        public string? CurrentExecutablePath
        {
            get;
            private set;
        }

        public string? ReplacementExecutablePath
        {
            get;
            private set;
        }

        public Task<bool> CanSelfUpdateAsync(
            string currentExecutablePath,
            CancellationToken cancellationToken)
        {
            CurrentExecutablePath =
                currentExecutablePath;

            return Task.FromResult(
                CanSelfUpdate);
        }

        public Task<VerificationResult> VerifyReplacementAsync(
            string currentExecutablePath,
            string replacementExecutablePath,
            CancellationToken cancellationToken)
        {
            CurrentExecutablePath =
                currentExecutablePath;

            ReplacementExecutablePath =
                replacementExecutablePath;

            return Task.FromResult(
                Verification);
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
