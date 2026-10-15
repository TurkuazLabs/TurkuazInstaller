// Dosya Yolu: REPO_ROOT/tests/TurkuazInstaller.Authenticode.LabProbe/Program.cs
// Amac: Gercek Windows Authenticode verification sonucunu immutable LAB JSON'a yazar
// Modul - Test Tool CSharp
// Version: 1.0.3
// Aciklama: Veri/sertifika deposuna dokunmaz, install/apply veya release kabul etmez
// Bagimli Oldugu Katman: Tool | Platform.Windows | Domain

using System.Security.Cryptography;
using System.Text.Json;
using TurkuazInstaller.Domain.Artifacts;
using TurkuazInstaller.Platform.Windows.Tools;

try
{
    if (!OperatingSystem.IsWindows())
    {
        throw new PlatformNotSupportedException("Windows LAB VM required.");
    }

    if (args.Length != 10 ||
        args[0] != "--artifact" ||
        args[2] != "--publisher" ||
        args[4] != "--cert-sha256" ||
        args[6] != "--case" ||
        args[8] != "--output")
    {
        throw new ArgumentException(
            "Required: --artifact PATH --publisher SUBJECT --cert-sha256 HEX --case REV-XX --output PATH");
    }

    var artifact = Path.GetFullPath(args[1]);
    var output = Path.GetFullPath(args[9]);
    var caseId = args[7];
    if (!System.Text.RegularExpressions.Regex.IsMatch(
        caseId, @"^REV-(0[1-9]|10|11)$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant))
    {
        throw new ArgumentException("Invalid LAB case id.");
    }

    if (!File.Exists(artifact))
    {
        throw new FileNotFoundException("LAB artifact missing.", artifact);
    }

    if (!Directory.Exists(Path.GetDirectoryName(output)) || File.Exists(output))
    {
        throw new IOException("LAB output directory must exist and output must not exist.");
    }

    var descriptor = new ArtifactSignatureDescriptor(
        ArtifactSignatureAlgorithm.Authenticode,
        args[3],
        args[5]);
    if (descriptor.CertificateSha256 is null)
    {
        throw new ArgumentException("External certificate SHA-256 pin is mandatory in LAB.");
    }

    // Retain a read-only handle that denies concurrent writers/deleters on
    // Windows for the duration of the native verification and both hashes.
    await using var artifactStream = new FileStream(
        artifact, FileMode.Open, FileAccess.Read, FileShare.Read);
    var initialSize = artifactStream.Length;
    var initialHash = await SHA256.HashDataAsync(artifactStream);
    var started = DateTimeOffset.UtcNow;
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var result = await new WindowsAuthenticodeArtifactSignatureVerifier()
        .VerifyAsync(artifact, descriptor, CancellationToken.None);
    stopwatch.Stop();

    artifactStream.Position = 0;
    var artifactSize = artifactStream.Length;
    var artifactHash = await SHA256.HashDataAsync(artifactStream);
    if (initialSize != artifactSize ||
        !CryptographicOperations.FixedTimeEquals(initialHash, artifactHash))
    {
        throw new IOException("LAB artifact changed during verification; evidence rejected.");
    }

    var evidence = new
    {
        schema_version = 1,
        case_id = caseId,
        observed_utc = started,
        artifact_sha256 = Convert.ToHexString(artifactHash).ToLowerInvariant(),
        artifact_size_bytes = artifactSize,
        verifier = "WindowsAuthenticodeArtifactSignatureVerifier",
        installer_winverifytrust_result = result.IsValid ? "ACCEPTED" : "REJECTED",
        failure = result.Failure.ToString(),
        message = result.Message,
        elapsed_ms = stopwatch.ElapsedMilliseconds,
        stage_apply = "NOT_RUN",
        release_acceptance = "NOT_ACCEPTED",
        note = "LAB-only adapter check; does not establish CRL/OCSP availability or production acceptance."
    };

    await using (var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        await JsonSerializer.SerializeAsync(file, evidence, new JsonSerializerOptions { WriteIndented = true });
        await file.FlushAsync();
    }

    Console.WriteLine("LAB_AUTHENTICODE_EVIDENCE_WRITTEN_NOT_RELEASE_ACCEPTANCE");
}
catch (Exception error)
{
    Console.Error.WriteLine("LAB Authenticode probe failed closed: " + error.Message);
    Environment.ExitCode = 1;
}
