// Dosya Yolu: /tests/TurkuazInstaller.Inventory.LabProbe/Program.cs
// Amac: CMS-signed release'deki full nupkg byte'larindan deterministik unsigned inventory olusturur
// Modul - Test Tool CSharp
// Version: 1.0.0
// Aciklama: LAB-only probe; CMS signer/private key kullanmaz, mevcut trust store'u sadece okur
// Bagimli Oldugu Katman: Domain | Infrastructure | Tool

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.File;

try
{
    var options = Parse(args);
    var package = PackageId.Parse(options["--package"]);
    var channel = options["--channel"] switch
    {
        "stable" => ReleaseChannel.Stable,
        "beta" => ReleaseChannel.Beta,
        _ => throw new ArgumentException("Channel must be stable or beta.")
    };
    var manifest = Path.GetFullPath(options["--manifest"]);
    var trust = Path.GetFullPath(options["--trust"]);
    var fullPackage = Path.GetFullPath(options["--full-package"]);
    var output = Path.GetFullPath(options["--output"]);

    // The trust anchor MUST have been supplied independently of the
    // signed manifest and cannot be synthesized from its signer.
    var trustRepository = new YamlManifestTrustPolicyRepository(
        new YamlManifestTrustPolicyRepositoryOptions(trust));
    var provider = new FileReleaseProvider(
        new InstallerManifestReader(),
        new CmsManifestSignatureVerifier(trustRepository),
        new FileReleaseProviderOptions(manifest, manifest));

    var release = await provider.GetLatestReleaseAsync(
        package, channel, CancellationToken.None)
        ?? throw new InvalidDataException("Pinned signed release was not resolved.");

    var inventory = await new VelopackPayloadInventoryBuilder()
        .BuildUnsignedAsync(fullPackage, release, CancellationToken.None);

    // Generate unsigned *bytes* only, never import/export signing keys,
    // write trust policies, install packages or approve recovery.
    // The independent release workflow must sign these exact bytes.
    if (File.Exists(output))
    {
        throw new IOException("Refusing to overwrite a payload inventory.");
    }

    await using (var stream = new FileStream(
        output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        await stream.WriteAsync(inventory);
        await stream.FlushAsync();
    }

    Console.WriteLine(
        "TURKUAZ_INSTALLER_TRUSTED_FULL_PACKAGE_UNSIGNED_INVENTORY_OK " +
        inventory.Length + " bytes");
}
catch (Exception error)
{
    Console.Error.WriteLine(
        "LAB inventory derivation failed closed: " + error.Message);
    Environment.ExitCode = 1;
}

static Dictionary<string, string> Parse(string[] args)
{
    var allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "--package", "--channel", "--manifest", "--trust",
        "--full-package", "--output"
    };
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    if (args.Length != allowed.Count * 2)
    {
        throw new ArgumentException("Six named LAB inventory arguments are required.");
    }

    for (var i = 0; i < args.Length; i += 2)
    {
        var key = args[i];
        var value = args[i + 1];
        if (!allowed.Contains(key) || options.ContainsKey(key) ||
            string.IsNullOrWhiteSpace(value) ||
            value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected, duplicated or empty inventory argument.");
        }
        options.Add(key, value);
    }
    return options;
}
