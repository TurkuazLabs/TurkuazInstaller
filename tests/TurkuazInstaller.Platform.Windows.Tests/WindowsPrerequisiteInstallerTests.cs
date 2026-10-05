// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsPrerequisiteInstallerTests.cs
// 📌 Amac: Windows prerequisite installer normal process, UAC ve reboot fail-closed davranislarini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.1
// Aciklama: ArgumentList aktarimi, explicit elevation, UAC iptali ve reboot-required exit kodlarini kapsar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Prerequisites;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsPrerequisiteInstallerTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task InstallAsync_Unelevated_UsesShellFreeProcessCommand()
    {
        var processRunner =
            new StubProcessRunner(
                new ProcessResult(
                    0,
                    string.Empty,
                    string.Empty));

        var elevatedRunner =
            new StubElevatedProcessRunner(
                new ElevationResult(
                    ElevationStatus.Completed,
                    0));

        var installer =
            new WindowsPrerequisiteInstaller(
                processRunner,
                elevatedRunner);

        var path =
            CreateTemporaryExecutable();

        try
        {
            await installer.InstallAsync(
                path,
                CreateInstallAction(
                    requiresElevation: false),
                CancellationToken.None);

            Assert.NotNull(
                processRunner.Command);

            Assert.Equal(
                path,
                processRunner.Command!.FileName);

            Assert.Equal(
                new[]
                {
                    "/quiet",
                    "/norestart"
                },
                processRunner.Command.Arguments);

            Assert.Null(
                elevatedRunner.Request);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InstallAsync_NonExeArtifact_FailsClosed()
    {
        var installer =
            new WindowsPrerequisiteInstaller(
                new StubProcessRunner(
                    new ProcessResult(
                        0,
                        string.Empty,
                        string.Empty)),
                new StubElevatedProcessRunner(
                    new ElevationResult(
                        ElevationStatus.Completed,
                        0)));

        var path =
            CreateTemporaryExecutable();

        var nonExePath =
            Path.ChangeExtension(
                path,
                ".cmd");

        File.Move(
            path,
            nonExePath);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    installer.InstallAsync(
                        nonExePath,
                        CreateInstallAction(
                            requiresElevation: true),
                        CancellationToken.None));
        }
        finally
        {
            File.Delete(
                nonExePath);
        }
    }

    [Fact]
    public async Task InstallAsync_Elevated_UsesExplicitElevationPort()
    {
        var processRunner =
            new StubProcessRunner(
                new ProcessResult(
                    0,
                    string.Empty,
                    string.Empty));

        var elevatedRunner =
            new StubElevatedProcessRunner(
                new ElevationResult(
                    ElevationStatus.Completed,
                    0));

        var installer =
            new WindowsPrerequisiteInstaller(
                processRunner,
                elevatedRunner);

        var path =
            CreateTemporaryExecutable();

        try
        {
            await installer.InstallAsync(
                path,
                CreateInstallAction(
                    requiresElevation: true),
                CancellationToken.None);

            Assert.NotNull(
                elevatedRunner.Request);

            Assert.Equal(
                path,
                elevatedRunner.Request!.FileName);

            Assert.Null(
                processRunner.Command);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InstallAsync_RebootRequired_FailsClosed()
    {
        var installer =
            new WindowsPrerequisiteInstaller(
                new StubProcessRunner(
                    new ProcessResult(
                        3010,
                        string.Empty,
                        string.Empty)),
                new StubElevatedProcessRunner(
                    new ElevationResult(
                        ElevationStatus.Completed,
                        0)));

        var path =
            CreateTemporaryExecutable();

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    installer.InstallAsync(
                        path,
                        CreateInstallAction(
                            requiresElevation: false),
                        CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InstallAsync_ElevationCancelled_FailsClosed()
    {
        var installer =
            new WindowsPrerequisiteInstaller(
                new StubProcessRunner(
                    new ProcessResult(
                        0,
                        string.Empty,
                        string.Empty)),
                new StubElevatedProcessRunner(
                    new ElevationResult(
                        ElevationStatus.Cancelled,
                        null)));

        var path =
            CreateTemporaryExecutable();

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    installer.InstallAsync(
                        path,
                        CreateInstallAction(
                            requiresElevation: true),
                        CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTemporaryExecutable()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "prerequisite.exe");

        File.WriteAllBytes(
            path,
            Array.Empty<byte>());

        return path;
    }

    private static PrerequisiteInstallAction CreateInstallAction(
        bool requiresElevation)
    {
        return new PrerequisiteInstallAction(
            new ArtifactDescriptor(
                new Uri(
                    "https://example.invalid/prerequisite.exe"),
                ArtifactDigest.ParseSha256(
                    Digest),
                1,
                new ArtifactSignatureDescriptor(
                    ArtifactSignatureAlgorithm.Authenticode,
                    "CN=Example Publisher")),
            new[]
            {
                "/quiet",
                "/norestart"
            },
            requiresElevation);
    }

    private sealed class StubProcessRunner
        : IProcessRunner
    {
        private readonly ProcessResult _result;

        public StubProcessRunner(
            ProcessResult result)
        {
            _result = result;
        }

        public ProcessCommand? Command { get; private set; }

        public Task<ProcessResult> RunAsync(
            ProcessCommand command,
            CancellationToken cancellationToken)
        {
            Command = command;
            return Task.FromResult(
                _result);
        }
    }

    private sealed class StubElevatedProcessRunner
        : IElevatedProcessRunner
    {
        private readonly ElevationResult _result;

        public StubElevatedProcessRunner(
            ElevationResult result)
        {
            _result = result;
        }

        public ElevationRequest? Request { get; private set; }

        public Task<ElevationResult> RunElevatedAsync(
            ElevationRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(
                _result);
        }
    }
}
