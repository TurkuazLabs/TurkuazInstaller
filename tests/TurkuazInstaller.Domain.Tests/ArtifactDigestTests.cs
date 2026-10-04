// 📄 Dosya Yolu: /tests/TurkuazInstaller.Domain.Tests/ArtifactDigestTests.cs
// 📌 Amac: ArtifactDigest SHA-256 uzunluk ve hex kurallarini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 0.3.0
// Aciklama: Gecerli digest normalizasyonunu ve gecersiz digest reddini kapsar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Artifacts;
using Xunit;

namespace TurkuazInstaller.Domain.Tests;

public sealed class ArtifactDigestTests
{
    private const string ValidDigest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void ParseSha256_AcceptsValidDigest()
    {
        var digest = ArtifactDigest.ParseSha256(ValidDigest.ToUpperInvariant());
        Assert.Equal(ValidDigest, digest.Sha256);
    }

    [Fact]
    public void ParseSha256_RejectsShortDigest()
    {
        Assert.Throws<FormatException>(() => ArtifactDigest.ParseSha256("0123"));
    }
}
