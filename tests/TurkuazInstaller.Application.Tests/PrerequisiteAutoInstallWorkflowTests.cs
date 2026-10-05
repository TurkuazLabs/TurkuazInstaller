// 📄 Dosya Yolu: /tests/TurkuazInstaller.Application.Tests/PrerequisiteAutoInstallWorkflowTests.cs
// 📌 Amac: Eksik prerequisite auto-install, verification ve zorunlu post-install re-probe akisini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Guvenli prerequisite install basarisi, policy eksigi ve re-probe basarisizligi durumlarinda package apply sinirini test eder
//
// Bagimli Oldugu Katman: Service | Repo | Tool

using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Contracts.Artifacts;
using TurkuazInstaller.Contracts.Packages;
using TurkuazInstaller.Contracts.State;
using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Prerequisites;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Domain.Verification;
using Xunit;

namespace TurkuazInstaller.Application.Tests;

public sealed class PrerequisiteAutoInstallWorkflowTests
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task InstallAsync_MissingPrerequisite_AutoInstallsAndReprobesBeforeApply()
    {
        var probe =
            new SequencedPrerequisiteProbe(
                false,
                true);

        var prerequisiteInstaller =
            new TrackingPrerequisiteInstaller();

        var packageEngine =
            new TrackingPackageEngine();

        var service =
            CreateService(
                probe,
                prerequisiteInstaller,
                packageEngine);

        await service.InstallAsync(
            CreateRelease(
                includeInstallAction: true),
            "C:/Apps/Example",
            "C:/Temp/TurkuazInstaller",
            null,
            CancellationToken.None);

        Assert.Equal(
            2,
            probe.Calls);

        Assert.Equal(
            1,
            prerequisiteInstaller.Calls);

        Assert.Equal(
            1,
            packageEngine.ApplyCalls);
    }

    [Fact]
    public async Task InstallAsync_MissingPrerequisiteWithoutInstallPolicy_StopsBeforeApply()
    {
        var probe =
            new SequencedPrerequisiteProbe(
                false);

        var prerequisiteInstaller =
            new TrackingPrerequisiteInstaller();

        var packageEngine =
            new TrackingPackageEngine();

        var service =
            CreateService(
                probe,
                prerequisiteInstaller,
                packageEngine);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.InstallAsync(
                    CreateRelease(
                        includeInstallAction: false),
                    "C:/Apps/Example",
                    "C:/Temp/TurkuazInstaller",
                    null,
                    CancellationToken.None));

        Assert.Equal(
            0,
            prerequisiteInstaller.Calls);

        Assert.Equal(
            0,
            packageEngine.ApplyCalls);
    }

    [Fact]
    public async Task InstallAsync_AutoInstallStillUnsatisfied_StopsBeforeApply()
    {
        var probe =
            new SequencedPrerequisiteProbe(
                false,
                false);

        var prerequisiteInstaller =
            new TrackingPrerequisiteInstaller();

        var packageEngine =
            new TrackingPackageEngine();

        var service =
            CreateService(
                probe,
                prerequisiteInstaller,
                packageEngine);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.InstallAsync(
                    CreateRelease(
                        includeInstallAction: true),
                    "C:/Apps/Example",
                    "C:/Temp/TurkuazInstaller",
                    null,
                    CancellationToken.None));

        Assert.Equal(
            2,
            probe.Calls);

        Assert.Equal(
            1,
            prerequisiteInstaller.Calls);

        Assert.Equal(
            0,
            packageEngine.ApplyCalls);
    }

    private static InstallerWorkflowService CreateService(
        ISystemPrerequisiteProbe prerequisiteProbe,
        IPrerequisiteInstaller prerequisiteInstaller,
        TrackingPackageEngine packageEngine)
    {
        return new InstallerWorkflowService(
            new StubDownloader(),
            new StubArtifactVerifier(),
            packageEngine,
            new StubStateRepository(),
            signatureVerifier:
                new StubSignatureVerifier(),
            prerequisiteProbe:
                prerequisiteProbe,
            prerequisiteInstaller:
                prerequisiteInstaller);
    }

    private static PackageRelease CreateRelease(
        bool includeInstallAction)
    {
        var prerequisite =
            new Prerequisite(
                PrerequisiteIds.DotNetDesktopRuntime,
                ">=10.0.0",
                includeInstallAction
                    ? CreateInstallAction()
                    : null);

        return new PackageRelease(
            PackageId.Parse(
                "example-app"),
            SemanticVersion.Parse(
                "1.0.0"),
            ReleaseChannel.Stable,
            new ArtifactDescriptor(
                new Uri(
                    "https://example.invalid/Example-Setup.exe"),
                ArtifactDigest.ParseSha256(
                    Digest),
                1024),
            new PackageInstallPolicy(
                PackageInstallMode.Full,
                "C:/Apps/Example",
                new[]
                {
                    prerequisite
                },
                Array.Empty<string>()),
            PackageRollbackPolicy.Disabled);
    }

    private static PrerequisiteInstallAction CreateInstallAction()
    {
        return new PrerequisiteInstallAction(
            new ArtifactDescriptor(
                new Uri(
                    "https://example.invalid/windowsdesktop-runtime.exe"),
                ArtifactDigest.ParseSha256(
                    Digest),
                2048,
                new ArtifactSignatureDescriptor(
                    ArtifactSignatureAlgorithm.Authenticode,
                    "CN=Prerequisite Publisher")),
            new[]
            {
                "/install",
                "/quiet",
                "/norestart"
            },
            requiresElevation: true);
    }

    private sealed class SequencedPrerequisiteProbe
        : ISystemPrerequisiteProbe
    {
        private readonly IReadOnlyList<bool> _results;

        public SequencedPrerequisiteProbe(
            params bool[] results)
        {
            _results = results;
        }

        public int Calls { get; private set; }

        public Task<bool> IsSatisfiedAsync(
            Prerequisite prerequisite,
            CancellationToken cancellationToken)
        {
            var index =
                Math.Min(
                    Calls,
                    _results.Count - 1);

            Calls++;

            return Task.FromResult(
                _results[index]);
        }
    }

    private sealed class TrackingPrerequisiteInstaller
        : IPrerequisiteInstaller
    {
        public int Calls { get; private set; }

        public Task InstallAsync(
            string verifiedInstallerPath,
            PrerequisiteInstallAction installAction,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubDownloader
        : IArtifactDownloader
    {
        public Task<string> DownloadAsync(
            ArtifactDescriptor artifact,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                "C:/Temp/Downloaded.exe");
        }
    }

    private sealed class StubArtifactVerifier
        : IArtifactVerifier
    {
        public Task<VerificationResult> VerifyAsync(
            string artifactPath,
            ArtifactDescriptor expectedArtifact,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                VerificationResult.Passed());
        }
    }

    private sealed class StubSignatureVerifier
        : IArtifactSignatureVerifier
    {
        public Task<VerificationResult> VerifyAsync(
            string artifactPath,
            ArtifactSignatureDescriptor expectedSignature,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                VerificationResult.Passed());
        }
    }

    private sealed class TrackingPackageEngine
        : IPackageEngine
    {
        public int ApplyCalls { get; private set; }

        public Task<PackageStage> StageAsync(
            PackageRelease release,
            string verifiedArtifactPath,
            string stagingDirectory,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new PackageStage(
                    release.PackageId,
                    release.Version,
                    PackageArtifactKind.VelopackSetup,
                    verifiedArtifactPath));
        }

        public Task ApplyAsync(
            InstallPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
        {
            ApplyCalls++;
            return Task.CompletedTask;
        }

        public Task RepairAsync(
            RepairPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RollbackAsync(
            RollbackPlan plan,
            PackageStage stage,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UninstallAsync(
            UninstallPlan plan,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubStateRepository
        : IInstallStateRepository
    {
        public Task<InstalledPackageState?> GetAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<InstalledPackageState?>(
                null);
        }

        public Task SaveAsync(
            InstalledPackageState state,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            PackageId packageId,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
