// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/VelopackPackageEngineTests.cs
// 📌 Amac: Velopack Package Engine staging, apply, repair ve rollback kontratini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 0.6.0
// Aciklama: Full/delta staging, delta reconstruction, resmi CLI argumentleri, atomic staging ve release eslesmesini test eder
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Packages.Velopack;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class VelopackPackageEngineTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string BasePackageFileName =
        "Example-1.0.0-full.nupkg";

    private const string DeltaPackageFileName =
        "Example-1.1.0-delta.nupkg";

    private const string PackagesDirectoryName =
        "packages";

    [Fact]
    public async Task StageAsync_SetupArtifact_CopiesAtomically()
    {
        using var fixture = new PackageEngineFixture();
        var source = fixture.CreateArtifact("Example-Setup.exe");
        var release = CreateRelease(
            "1.0.0",
            "https://downloads.example.invalid/Example-Setup.exe");

        var engine = new VelopackPackageEngine(
            fixture.ProcessRunner);

        var stage = await engine.StageAsync(
            new PackageStageRequest(
                release,
                release.Artifact),
            source,
            fixture.StagingDirectory,
            CancellationToken.None);

        Assert.Equal(
            PackageArtifactKind.VelopackSetup,
            stage.ArtifactKind);

        Assert.True(File.Exists(stage.ArtifactPath));
        Assert.Equal(
            "payload",
            await File.ReadAllTextAsync(stage.ArtifactPath));
    }

    [Fact]
    public async Task ApplyAsync_SetupArtifact_UsesOfficialInstallArguments()
    {
        using var fixture = new PackageEngineFixture();
        var source = fixture.CreateArtifact("Example-Setup.exe");
        var release = CreateRelease(
            "1.0.0",
            "https://downloads.example.invalid/Example-Setup.exe");

        var engine = new VelopackPackageEngine(
            fixture.ProcessRunner);

        var stage = await engine.StageAsync(
            new PackageStageRequest(
                release,
                release.Artifact),
            source,
            fixture.StagingDirectory,
            CancellationToken.None);

        var plan = new InstallPlan(
            release,
            fixture.InstallDirectory,
            Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
            new[] { "UserData" });

        await engine.ApplyAsync(
            plan,
            stage,
            CancellationToken.None);

        var command = Assert.IsType<ProcessCommand>(
            fixture.ProcessRunner.LastCommand);

        Assert.Equal(stage.ArtifactPath, command.FileName);
        Assert.Equal(
            new[]
            {
                "--silent",
                "--installto",
                Path.GetFullPath(fixture.InstallDirectory)
            },
            command.Arguments);
    }

    [Fact]
    public async Task ApplyAsync_FullPackage_UsesUpdateExecutable()
    {
        using var fixture = new PackageEngineFixture();
        fixture.CreateUpdater();

        var source = fixture.CreateArtifact("Example-1.1.0-full.nupkg");
        var release = CreateRelease(
            "1.1.0",
            "https://downloads.example.invalid/Example-1.1.0-full.nupkg");

        var engine = new VelopackPackageEngine(
            fixture.ProcessRunner);

        var stage = await engine.StageAsync(
            new PackageStageRequest(
                release,
                release.Artifact),
            source,
            fixture.StagingDirectory,
            CancellationToken.None);

        var plan = new InstallPlan(
            release,
            fixture.InstallDirectory,
            Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
            Array.Empty<string>());

        await engine.ApplyAsync(
            plan,
            stage,
            CancellationToken.None);

        var command = Assert.IsType<ProcessCommand>(
            fixture.ProcessRunner.LastCommand);

        var installRoot = Path.GetFullPath(
            fixture.InstallDirectory);

        var packagesDirectory = Path.Combine(
            installRoot,
            "packages");

        var copiedPackage = Path.Combine(
            packagesDirectory,
            Path.GetFileName(stage.ArtifactPath));

        Assert.Equal(
            Path.Combine(installRoot, "Update.exe"),
            command.FileName);

        Assert.Equal(
            new[]
            {
                "--silent",
                "--rootDir",
                installRoot,
                "--packageDir",
                packagesDirectory,
                "apply",
                "--norestart",
                "--package",
                copiedPackage
            },
            command.Arguments);

        Assert.True(File.Exists(copiedPackage));
    }

    [Fact]
    public async Task StageAsync_DeltaArtifact_ReconstructsFullPackage()
    {
        using var fixture =
            new PackageEngineFixture();

        fixture.CreateUpdater();

        var basePackage =
            fixture.CreateInstalledPackage(
                BasePackageFileName);

        var source =
            fixture.CreateArtifact(
                DeltaPackageFileName);

        var release =
            CreateDeltaRelease();

        fixture.ProcessRunner.BeforeReturn =
            command =>
            {
                File.WriteAllText(
                    command.Arguments[^1],
                    "reconstructed");
            };

        var engine =
            new VelopackPackageEngine(
                fixture.ProcessRunner);

        var stage =
            await engine.StageAsync(
                new PackageStageRequest(
                    release,
                    release.DeltaArtifact!.Artifact,
                    fixture.InstallDirectory),
                source,
                fixture.StagingDirectory,
                CancellationToken.None);

        Assert.Equal(
            PackageArtifactKind.VelopackFullPackage,
            stage.ArtifactKind);

        Assert.True(
            File.Exists(
                stage.ArtifactPath));

        var command =
            Assert.IsType<ProcessCommand>(
                fixture.ProcessRunner.LastCommand);

        Assert.Equal(
            Path.Combine(
                Path.GetFullPath(
                    fixture.InstallDirectory),
                "Update.exe"),
            command.FileName);

        Assert.Contains(
            basePackage,
            command.Arguments);

        Assert.Contains(
            stage.ArtifactPath,
            command.Arguments);
    }

    [Fact]
    public async Task RepairAsync_RequiresFullPackage()
    {
        using var fixture = new PackageEngineFixture();
        var source = fixture.CreateArtifact("Example-Setup.exe");
        var release = CreateRelease(
            "1.0.0",
            "https://downloads.example.invalid/Example-Setup.exe");

        var engine = new VelopackPackageEngine(
            fixture.ProcessRunner);

        var stage = await engine.StageAsync(
            new PackageStageRequest(
                release,
                release.Artifact),
            source,
            fixture.StagingDirectory,
            CancellationToken.None);

        var plan = new RepairPlan(
            release,
            fixture.InstallDirectory);

        var exception = await Assert.ThrowsAsync<PackageEngineException>(
            () => engine.RepairAsync(
                plan,
                stage,
                CancellationToken.None));

        Assert.Equal(
            PackageEngineOperation.Repair,
            exception.Operation);
    }

    [Fact]
    public async Task RollbackAsync_UsesPreviousReleasePackage()
    {
        using var fixture = new PackageEngineFixture();
        fixture.CreateUpdater();

        var source = fixture.CreateArtifact("Example-1.0.0-full.nupkg");
        var previous = CreateRelease(
            "1.0.0",
            "https://downloads.example.invalid/Example-1.0.0-full.nupkg");

        var current = CreateRelease(
            "1.1.0",
            "https://downloads.example.invalid/Example-1.1.0-full.nupkg");

        var engine = new VelopackPackageEngine(
            fixture.ProcessRunner);

        var stage = await engine.StageAsync(
            new PackageStageRequest(
                previous,
                previous.Artifact),
            source,
            fixture.StagingDirectory,
            CancellationToken.None);

        var plan = new RollbackPlan(
            current,
            previous,
            fixture.InstallDirectory);

        await engine.RollbackAsync(
            plan,
            stage,
            CancellationToken.None);

        Assert.NotNull(
            fixture.ProcessRunner.LastCommand);
    }

    [Fact]
    public async Task UninstallAsync_UsesOfficialUninstallCommand()
    {
        using var fixture =
            new PackageEngineFixture();

        fixture.CreateUpdater();

        var engine =
            new VelopackPackageEngine(
                fixture.ProcessRunner);

        var plan =
            new UninstallPlan(
                PackageId.Parse("example-app"),
                SemanticVersion.Parse("1.0.0"),
                fixture.InstallDirectory);

        await engine.UninstallAsync(
            plan,
            CancellationToken.None);

        var command =
            Assert.IsType<ProcessCommand>(
                fixture.ProcessRunner.LastCommand);

        var installRoot =
            Path.GetFullPath(
                fixture.InstallDirectory);

        Assert.Equal(
            Path.Combine(
                installRoot,
                "Update.exe"),
            command.FileName);

        Assert.Equal(
            new[]
            {
                "--silent",
                "--rootDir",
                installRoot,
                "uninstall"
            },
            command.Arguments);
    }

    [Fact]
    public async Task ApplyAsync_RejectsPreservePathInsideCurrent()
    {
        using var fixture = new PackageEngineFixture();
        fixture.CreateUpdater();

        var source = fixture.CreateArtifact("Example-1.1.0-full.nupkg");
        var release = CreateRelease(
            "1.1.0",
            "https://downloads.example.invalid/Example-1.1.0-full.nupkg");

        var engine = new VelopackPackageEngine(
            fixture.ProcessRunner);

        var stage = await engine.StageAsync(
            new PackageStageRequest(
                release,
                release.Artifact),
            source,
            fixture.StagingDirectory,
            CancellationToken.None);

        var plan = new InstallPlan(
            release,
            fixture.InstallDirectory,
            Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
            new[] { Path.Combine("current", "settings.json") });

        var exception = await Assert.ThrowsAsync<PackageEngineException>(
            () => engine.ApplyAsync(
                plan,
                stage,
                CancellationToken.None));

        Assert.Equal(
            PackageEngineOperation.Apply,
            exception.Operation);
    }

    [Fact]
    public async Task ApplyAsync_NonZeroProcessExit_ThrowsTypedError()
    {
        using var fixture = new PackageEngineFixture();
        fixture.ProcessRunner.Result = new ProcessResult(
            17,
            string.Empty,
            string.Empty);

        var source = fixture.CreateArtifact("Example-Setup.exe");
        var release = CreateRelease(
            "1.0.0",
            "https://downloads.example.invalid/Example-Setup.exe");

        var engine = new VelopackPackageEngine(
            fixture.ProcessRunner);

        var stage = await engine.StageAsync(
            new PackageStageRequest(
                release,
                release.Artifact),
            source,
            fixture.StagingDirectory,
            CancellationToken.None);

        var plan = new InstallPlan(
            release,
            fixture.InstallDirectory,
            Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
            Array.Empty<string>());

        var exception = await Assert.ThrowsAsync<PackageEngineException>(
            () => engine.ApplyAsync(
                plan,
                stage,
                CancellationToken.None));

        Assert.Equal(17, exception.ExitCode);
    }

    private static PackageRelease CreateDeltaRelease()
    {
        var fullArtifact =
            new ArtifactDescriptor(
                new Uri(
                    "https://downloads.example.invalid/Example-1.1.0-full.nupkg"),
                ArtifactDigest.ParseSha256(
                    Digest),
                2048);

        var deltaArtifact =
            new PackageDeltaArtifact(
                SemanticVersion.Parse(
                    "1.0.0"),
                new ArtifactDescriptor(
                    new Uri(
                        "https://downloads.example.invalid/Example-1.1.0-delta.nupkg"),
                    ArtifactDigest.ParseSha256(
                        Digest),
                    512));

        return new PackageRelease(
            PackageId.Parse(
                "example-app"),
            SemanticVersion.Parse(
                "1.1.0"),
            ReleaseChannel.Stable,
            fullArtifact,
            PackageInstallPolicy.LegacyDefault,
            PackageRollbackPolicy.Disabled,
            deltaArtifact);
    }

    private static PackageRelease CreateRelease(
        string version,
        string artifactUri)
    {
        return new PackageRelease(
            PackageId.Parse("example-app"),
            SemanticVersion.Parse(version),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri(artifactUri),
                ArtifactDigest.ParseSha256(Digest),
                1024));
    }

    private sealed class PackageEngineFixture : IDisposable
    {
        private readonly string _rootDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstallerTests",
                Guid.NewGuid().ToString("N"));

        public PackageEngineFixture()
        {
            Directory.CreateDirectory(_rootDirectory);
            Directory.CreateDirectory(StagingDirectory);
            Directory.CreateDirectory(InstallDirectory);
        }

        public FakeProcessRunner ProcessRunner { get; } = new();

        public string StagingDirectory =>
            Path.Combine(
                _rootDirectory,
                "staging");

        public string InstallDirectory =>
            Path.Combine(
                _rootDirectory,
                "install");

        public string CreateArtifact(string fileName)
        {
            var path = Path.Combine(
                _rootDirectory,
                fileName);

            File.WriteAllText(
                path,
                "payload");

            return path;
        }

        public string CreateInstalledPackage(
            string fileName)
        {
            var packagesDirectory =
                Path.Combine(
                    InstallDirectory,
                    PackagesDirectoryName);

            Directory.CreateDirectory(
                packagesDirectory);

            var path =
                Path.Combine(
                    packagesDirectory,
                    fileName);

            File.WriteAllText(
                path,
                "base");

            return path;
        }

        public void CreateUpdater()
        {
            File.WriteAllText(
                Path.Combine(
                    InstallDirectory,
                    "Update.exe"),
                "stub");
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(
                    _rootDirectory,
                    recursive: true);
            }
        }
    }
}
