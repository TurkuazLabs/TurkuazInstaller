// Dosya: /tests/TurkuazInstaller.Velopack.E2E.Runner/SignedWorkflowAcceptanceRunner.cs
// Amac: Gecici CMS imzali local manifest ile gercek Velopack package engine ve kalici state lifecycle testini birlestirir
// Version: 0.1.0
// Guvenlik: Ephemeral CI certificate, external trust pin, unsigned fixture; production signing veya VM kabul testi degildir

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TurkuazInstaller.Application.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Domain.State;
using TurkuazInstaller.Infrastructure.Artifacts;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Packages.Velopack;
using TurkuazInstaller.Infrastructure.Processes;
using TurkuazInstaller.Infrastructure.Providers.File;
using TurkuazInstaller.Infrastructure.Repositories;

namespace TurkuazInstaller.Velopack.E2E.Runner;

internal static class SignedWorkflowAcceptanceRunner
{
    private const string PackageIdText = "turkuazlabs.turkuazinstaller.e2e";
    private const string CertificateSubject = "CN=TurkuazInstaller Ephemeral CI Fixture";
    private const string VersionOne = "1.0.0";
    private const string VersionTwo = "2.0.0";

    public static async Task RunAsync(
        string setupV1,
        string fullV1,
        string fullV2,
        string installRoot,
        string stagingRoot,
        string versionMarker,
        string userDataDirectory,
        string userDataMarkerFile)
    {
        var packageId = PackageId.Parse(PackageIdText);
        var manifestRoot = Path.Combine(stagingRoot, "manifests");
        var trustRoot = Path.Combine(stagingRoot, "external-trust");
        var stateRoot = Path.Combine(stagingRoot, "installed-state");
        var trustPath = Path.Combine(trustRoot, "manifest-trust.yml");

        if (Directory.Exists(stagingRoot))
        {
            Directory.Delete(stagingRoot, recursive: true);
        }
        if (Directory.Exists(installRoot))
        {
            Directory.Delete(installRoot, recursive: true);
        }
        Directory.CreateDirectory(manifestRoot);
        Directory.CreateDirectory(trustRoot);

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            CertificateSubject,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature, critical: true));

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        var certPin = certificate
            .GetCertHashString(HashAlgorithmName.SHA256)
            .ToLowerInvariant();

        await WriteTrustPolicyAsync(trustPath, certificate, certPin);

        var v1SetupManifest = Path.Combine(manifestRoot, "v1-install.yml");
        var v2Manifest = Path.Combine(manifestRoot, "v2-update.yml");
        var v1FullManifest = Path.Combine(manifestRoot, "v1-rollback.yml");
        var v2BadDigestManifest = Path.Combine(manifestRoot, "v2-wrong-hash.yml");

        await WriteSignedManifestAsync(v1SetupManifest, setupV1, VersionOne, certificate);
        await WriteSignedManifestAsync(v2Manifest, fullV2, VersionTwo, certificate);
        await WriteSignedManifestAsync(v1FullManifest, fullV1, VersionOne, certificate);
        await WriteSignedManifestAsync(
            v2BadDigestManifest, fullV2, VersionTwo, certificate, wrongDigest: true);

        var stateRepository = new JsonInstallStateRepository(
            new JsonInstallStateRepositoryOptions(stateRoot));
        var packageEngine = new VelopackPackageEngine(new SystemProcessRunner());
        using var httpClient = new HttpClient();
        var workflow = new InstallerWorkflowService(
            new DefaultArtifactDownloader(httpClient),
            new Sha256ArtifactVerifier(),
            packageEngine,
            stateRepository);

        async Task<PackageRelease> ReadVerifiedAsync(string manifestPath)
        {
            var trustRepo = new YamlManifestTrustPolicyRepository(
                new YamlManifestTrustPolicyRepositoryOptions(trustPath));
            var provider = new FileReleaseProvider(
                new InstallerManifestReader(),
                new CmsManifestSignatureVerifier(trustRepo),
                new FileReleaseProviderOptions(manifestPath, manifestPath));
            return await provider.GetLatestReleaseAsync(
                packageId, ReleaseChannel.Stable, CancellationToken.None)
                ?? throw new InvalidDataException("Signed manifest did not resolve a release.");
        }

        var releaseV1 = await ReadVerifiedAsync(v1SetupManifest);
        await workflow.InstallAsync(
            releaseV1, installRoot, stagingRoot, null, CancellationToken.None);
        await AssertVersionAsync(
            stateRepository, packageId, installRoot, versionMarker, VersionOne);

        var userDataPath = Path.Combine(
            installRoot, userDataDirectory, userDataMarkerFile);
        Directory.CreateDirectory(Path.GetDirectoryName(userDataPath)!);
        var userData = Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(userDataPath, userData);

        // The valid CMS is bound to exact manifest bytes: tampering must fail
        // before any new release reaches the package engine.
        var validV2Bytes = await File.ReadAllBytesAsync(v2Manifest);
        await File.AppendAllTextAsync(v2Manifest, "\n# unsigned tampering\n");
        await ExpectManifestRejectedAsync(() => ReadVerifiedAsync(v2Manifest));
        await File.WriteAllBytesAsync(v2Manifest, validV2Bytes);
        await AssertVersionAsync(
            stateRepository, packageId, installRoot, versionMarker, VersionOne);
        AssertUserData(userDataPath, userData);

        // A different external certificate pin must reject even a valid CMS.
        await WriteTrustPolicyAsync(trustPath, certificate, new string('0', 64));
        await ExpectManifestRejectedAsync(() => ReadVerifiedAsync(v2Manifest));
        await WriteTrustPolicyAsync(trustPath, certificate, certPin);
        await AssertVersionAsync(
            stateRepository, packageId, installRoot, versionMarker, VersionOne);

        // A correctly signed manifest with an intentionally wrong artifact
        // SHA-256 must not update an installed package or the persisted state.
        var badDigestRelease = await ReadVerifiedAsync(v2BadDigestManifest);
        try
        {
            await workflow.UpdateAsync(
                badDigestRelease,
                await RequireStateAsync(stateRepository, packageId),
                stagingRoot, null, CancellationToken.None);
            throw new InvalidOperationException(
                "A wrong SHA-256 signed manifest unexpectedly reached package apply.");
        }
        catch (InvalidDataException)
        {
            // Expected fail-closed integrity verification.
        }
        await AssertVersionAsync(
            stateRepository, packageId, installRoot, versionMarker, VersionOne);
        AssertUserData(userDataPath, userData);
        Console.WriteLine("TURKUAZ_INSTALLER_SIGNED_WORKFLOW_FAIL_CLOSED_OK");

        // The same real signed-manifest pipeline now drives real Velopack
        // update/repair/rollback and a persisted JSON state repository.
        var releaseV2 = await ReadVerifiedAsync(v2Manifest);
        await workflow.UpdateAsync(
            releaseV2,
            await RequireStateAsync(stateRepository, packageId),
            stagingRoot, null, CancellationToken.None);
        await AssertVersionAsync(
            stateRepository, packageId, installRoot, versionMarker, VersionTwo);
        AssertUserData(userDataPath, userData);

        await workflow.RepairAsync(
            releaseV2,
            await RequireStateAsync(stateRepository, packageId),
            stagingRoot, null, CancellationToken.None);
        await AssertVersionAsync(
            stateRepository, packageId, installRoot, versionMarker, VersionTwo);
        AssertUserData(userDataPath, userData);

        var rollbackRelease = await ReadVerifiedAsync(v1FullManifest);
        await workflow.RollbackAsync(
            releaseV2,
            rollbackRelease,
            await RequireStateAsync(stateRepository, packageId),
            stagingRoot, null, CancellationToken.None);
        await AssertVersionAsync(
            stateRepository, packageId, installRoot, versionMarker, VersionOne);
        AssertUserData(userDataPath, userData);

        await workflow.UninstallAsync(
            await RequireStateAsync(stateRepository, packageId),
            null,
            CancellationToken.None);
        if (File.Exists(Path.Combine(installRoot, "Update.exe")) ||
            Directory.Exists(Path.Combine(installRoot, "current")) ||
            await stateRepository.GetAsync(packageId, CancellationToken.None) is not null)
        {
            throw new InvalidOperationException(
                "Signed workflow uninstall left active application files or committed state.");
        }

        Console.WriteLine("TURKUAZ_INSTALLER_SIGNED_REAL_VELOPACK_WORKFLOW_E2E_OK");
    }

    private static async Task<InstalledPackageState> RequireStateAsync(
        JsonInstallStateRepository repository, PackageId packageId)
        => await repository.GetAsync(packageId, CancellationToken.None)
            ?? throw new InvalidOperationException("Expected committed install state.");

    private static async Task AssertVersionAsync(
        JsonInstallStateRepository repository,
        PackageId packageId,
        string installRoot,
        string versionMarker,
        string version)
    {
        var state = await RequireStateAsync(repository, packageId);
        var markerPath = Path.Combine(installRoot, "current", versionMarker);
        if (!string.Equals(state.Version.ToString(), version, StringComparison.Ordinal) ||
            !File.Exists(Path.Combine(installRoot, "Update.exe")) ||
            !File.Exists(markerPath) ||
            !string.Equals(File.ReadAllText(markerPath).Trim(), version, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Signed workflow committed state and real Velopack marker mismatch.");
        }
    }

    private static void AssertUserData(string markerPath, string expected)
    {
        if (!File.Exists(markerPath) ||
            !string.Equals(File.ReadAllText(markerPath), expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Signed workflow changed persistent user data.");
        }
    }

    private static async Task ExpectManifestRejectedAsync(
        Func<Task<PackageRelease>> resolveRelease)
    {
        try
        {
            await resolveRelease();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException(
            "Untrusted local manifest unexpectedly resolved a release.");
    }

    private static async Task WriteTrustPolicyAsync(
        string path, X509Certificate2 certificate, string certificatePin)
    {
        var lines = new[]
        {
            "schema_version: 1",
            "packages:",
            "  - id: " + PackageIdText,
            "    publisher_subject: \"" + certificate.Subject + "\"",
            "    certificate_sha256: " + certificatePin,
            ""
        };
        await File.WriteAllTextAsync(path, string.Join("\n", lines));
    }

    private static async Task WriteSignedManifestAsync(
        string path,
        string artifactPath,
        string version,
        X509Certificate2 certificate,
        bool wrongDigest = false)
    {
        using var stream = File.OpenRead(artifactPath);
        var digest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (wrongDigest)
        {
            digest = new string('0', 64);
        }

        var uri = new UriBuilder(Uri.UriSchemeFile, string.Empty)
        {
            Path = Path.GetFullPath(artifactPath)
        }.Uri.AbsoluteUri;

        var lines = new[]
        {
            "schema_version: 1",
            "package:",
            "  id: " + PackageIdText,
            "  version: " + version,
            "  channel: stable",
            "artifact:",
            "  uri: \"" + uri + "\"",
            "  sha256: " + digest,
            "  size_bytes: " + stream.Length,
            "install:",
            "  mode: full",
            "  target: C:/Apps/TurkuazInstallerCiFixture",
            "  preserve_paths:",
            "    - UserData",
            "rollback:",
            "  supported: true",
            "  previous_version_required: true",
            ""
        };

        var raw = Encoding.UTF8.GetBytes(string.Join("\n", lines));
        await File.WriteAllBytesAsync(path, raw);

        var signedCms = new SignedCms(new ContentInfo(raw), detached: true);
        signedCms.ComputeSignature(
            new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate)
            {
                IncludeOption = X509IncludeOption.EndCertOnly
            },
            silent: true);
        await File.WriteAllBytesAsync(path + ".p7s", signedCms.Encode());
    }
}
