// 📄 Dosya Yolu: /tests/TurkuazInstaller.Velopack.E2E.Runner/Program.cs
// 📌 Amac: Gercek Velopack artifactlariyla TurkuazInstaller Package Engine yasam dongusunu uctan uca dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Setup install, full-package update, repair, rollback ve uninstall sonrasi disk durumunu fail-fast kontrollerle test eder
//
// Bagimli Oldugu Katman: Service | Tool

using System.Security.Cryptography;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Domain.Plans;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Packages.Velopack;
using TurkuazInstaller.Infrastructure.Processes;

namespace TurkuazInstaller.Velopack.E2E.Runner;

internal static class Program
{
    private const int ExpectedArgumentCount = 6;
    private const string PackageIdValue = "turkuazlabs.turkuazinstaller.e2e";
    private const string VersionOne = "1.0.0";
    private const string VersionTwo = "2.0.0";

    public static async Task<int> Main(
        string[] args)
    {
        if (args.Length != ExpectedArgumentCount)
        {
            Console.Error.WriteLine(
                "Expected arguments: <v1-setup> <v1-full> <v2-full> <install-root> <staging-root> <version-marker>.");
            return 2;
        }

        var setupV1 = Path.GetFullPath(args[0]);
        var fullV1 = Path.GetFullPath(args[1]);
        var fullV2 = Path.GetFullPath(args[2]);
        var installRoot = Path.GetFullPath(args[3]);
        var stagingRoot = Path.GetFullPath(args[4]);
        var markerFile = args[5].Trim();

        var packageId = PackageId.Parse(PackageIdValue);
        var releaseV1 = CreateRelease(packageId, VersionOne, setupV1);
        var fullReleaseV1 = CreateRelease(packageId, VersionOne, fullV1);
        var releaseV2 = CreateRelease(packageId, VersionTwo, fullV2);
        var engine = new VelopackPackageEngine(new SystemProcessRunner());

        try
        {
            PrepareDirectory(stagingRoot);

            if (Directory.Exists(installRoot))
            {
                Directory.Delete(installRoot, recursive: true);
            }

            await InstallAsync(engine, releaseV1, installRoot, stagingRoot).ConfigureAwait(false);
            AssertInstalledVersion(installRoot, markerFile, VersionOne);

            await ApplyUpdateAsync(engine, releaseV2, installRoot, stagingRoot).ConfigureAwait(false);
            AssertInstalledVersion(installRoot, markerFile, VersionTwo);

            await RepairAsync(engine, releaseV2, installRoot, stagingRoot).ConfigureAwait(false);
            AssertInstalledVersion(installRoot, markerFile, VersionTwo);

            await RollbackAsync(engine, releaseV2, fullReleaseV1, installRoot, stagingRoot).ConfigureAwait(false);
            AssertInstalledVersion(installRoot, markerFile, VersionOne);

            await engine.UninstallAsync(
                new UninstallPlan(packageId, fullReleaseV1.Version, installRoot),
                CancellationToken.None).ConfigureAwait(false);

            AssertUninstalled(installRoot);

            Console.WriteLine("TURKUAZ_INSTALLER_VELOPACK_E2E_OK");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            await TryCleanupAsync(engine, packageId, installRoot).ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task InstallAsync(
        VelopackPackageEngine engine,
        PackageRelease release,
        string installRoot,
        string stagingRoot)
    {
        var stage = await engine.StageAsync(
            release,
            release.Artifact.Uri.LocalPath,
            stagingRoot,
            CancellationToken.None).ConfigureAwait(false);

        await engine.ApplyAsync(
            new InstallPlan(
                release,
                installRoot,
                Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
                Array.Empty<string>()),
            stage,
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task ApplyUpdateAsync(
        VelopackPackageEngine engine,
        PackageRelease release,
        string installRoot,
        string stagingRoot)
    {
        var stage = await engine.StageAsync(
            release,
            release.Artifact.Uri.LocalPath,
            stagingRoot,
            CancellationToken.None).ConfigureAwait(false);

        await engine.ApplyAsync(
            new InstallPlan(
                release,
                installRoot,
                Array.Empty<TurkuazInstaller.Domain.Prerequisites.Prerequisite>(),
                Array.Empty<string>()),
            stage,
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task RepairAsync(
        VelopackPackageEngine engine,
        PackageRelease release,
        string installRoot,
        string stagingRoot)
    {
        var stage = await engine.StageAsync(
            release,
            release.Artifact.Uri.LocalPath,
            stagingRoot,
            CancellationToken.None).ConfigureAwait(false);

        await engine.RepairAsync(
            new RepairPlan(release, installRoot),
            stage,
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task RollbackAsync(
        VelopackPackageEngine engine,
        PackageRelease currentRelease,
        PackageRelease previousRelease,
        string installRoot,
        string stagingRoot)
    {
        var stage = await engine.StageAsync(
            previousRelease,
            previousRelease.Artifact.Uri.LocalPath,
            stagingRoot,
            CancellationToken.None).ConfigureAwait(false);

        await engine.RollbackAsync(
            new RollbackPlan(currentRelease, previousRelease, installRoot),
            stage,
            CancellationToken.None).ConfigureAwait(false);
    }

    private static PackageRelease CreateRelease(
        PackageId packageId,
        string version,
        string artifactPath)
    {
        var fullPath = Path.GetFullPath(artifactPath);
        var digest = ComputeSha256(fullPath);

        var artifact = new ArtifactDescriptor(
            CreateFileUri(fullPath),
            ArtifactDigest.ParseSha256(digest),
            new FileInfo(fullPath).Length);

        return new PackageRelease(
            packageId,
            SemanticVersion.Parse(version),
            ReleaseChannel.Stable,
            artifact,
            PackageInstallPolicy.LegacyDefault,
            new PackageRollbackPolicy(
                Supported: true,
                PreviousVersionRequired: true));
    }

    private static Uri CreateFileUri(
        string path)
    {
        return new UriBuilder(
            Uri.UriSchemeFile,
            string.Empty)
        {
            Path = path
        }.Uri;
    }

    private static string ComputeSha256(
        string path)
    {
        using var stream = File.OpenRead(path);

        return Convert
            .ToHexString(SHA256.HashData(stream))
            .ToLowerInvariant();
    }

    private static void AssertInstalledVersion(
        string installRoot,
        string markerFile,
        string expectedVersion)
    {
        var updaterPath = Path.Combine(installRoot, "Update.exe");

        if (!File.Exists(updaterPath))
        {
            throw new InvalidOperationException(
                "Velopack Update.exe was not installed.");
        }

        var markerPath = Path.Combine(
            installRoot,
            "current",
            markerFile);

        if (!File.Exists(markerPath))
        {
            throw new InvalidOperationException(
                "Version marker was not installed.");
        }

        var actual = File.ReadAllText(markerPath).Trim();

        if (!string.Equals(actual, expectedVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                string.Concat(
                    "Installed version marker mismatch. Expected ",
                    expectedVersion,
                    ", actual ",
                    actual,
                    "."));
        }
    }

    private static void AssertUninstalled(
        string installRoot)
    {
        if (
            File.Exists(Path.Combine(installRoot, "Update.exe")) ||
            Directory.Exists(Path.Combine(installRoot, "current")))
        {
            throw new InvalidOperationException(
                "Velopack uninstall left active install files behind.");
        }
    }

    private static void PrepareDirectory(
        string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static async Task TryCleanupAsync(
        VelopackPackageEngine engine,
        PackageId packageId,
        string installRoot)
    {
        try
        {
            var updaterPath = Path.Combine(installRoot, "Update.exe");

            if (!File.Exists(updaterPath))
            {
                return;
            }

            await engine.UninstallAsync(
                new UninstallPlan(
                    packageId,
                    SemanticVersion.Parse(VersionOne),
                    installRoot),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
        }
    }
}
