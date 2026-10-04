// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Artifacts/ArtifactDigest.cs
// 📌 Amac: SHA-256 artifact digest degerini typed ve dogrulanmis bicimde temsil eder
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Install ve rollback oncesi 64 hex karakter integrity kurali uygular
//
// Bagimli Oldugu Katman: Service

using System.Text.RegularExpressions;

namespace TurkuazInstaller.Domain.Artifacts;

public sealed partial record ArtifactDigest
{
    private ArtifactDigest(string sha256) => Sha256 = sha256;

    public string Sha256 { get; }

    public static ArtifactDigest ParseSha256(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();

        if (!Sha256Pattern().IsMatch(normalized))
        {
            throw new FormatException("SHA-256 digest must contain exactly 64 hexadecimal characters.");
        }

        return new ArtifactDigest(normalized);
    }

    public override string ToString() => Sha256;

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();
}
